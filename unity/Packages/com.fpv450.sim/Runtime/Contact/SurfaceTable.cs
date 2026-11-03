using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Contact
{
    /// <summary>A surface's moisture set (D-021): Sunny reads Dry, Rain Wet, Severe rain Saturated; a cover kind reads Wet for both rains.</summary>
    public enum Moisture : byte { Dry = 0, Wet = 1, Saturated = 2 }

    /// <summary>The kinds the world's drawn elements are made of (p7a-ground design 8), in surfaces.json's cover order.</summary>
    public enum CoverKind : byte { Straw = 0, Stem = 1, Twig = 2, Leaf = 3 }

    /// <summary>
    /// One moisture set of a ground surface (surfaces.json, p7a-ground design 1): Bekker's linear pressure–sinkage at a
    /// contact of width b, p = (k_c/b + k_φ)·z, under Wong's load and unload rule, with the firm layer past the maximum
    /// sink, viscous damping, the friction of the drone's parts and the wet soil's adhesion. A rigid set (asphalt, a
    /// concrete slab) has friction only, every soil field 0.
    /// </summary>
    public struct SoilSet
    {
        public bool Rigid;

        /// <summary>The virgin-loading modulus at the table's reference width, k_c/b_ref + k_φ (N/m³), and k_c/b_ref's share of it.</summary>
        public double Bearing, CohesiveShare;

        /// <summary>Bekker's cohesive modulus k_c (N/m²) and frictional modulus k_φ (N/m³), n = 1.</summary>
        public double CohesiveModulus, FrictionalModulus;

        /// <summary>Wong's unload and reload stiffness over the virgin loading's (≥ 1), and the sink past which the firm layer adds, m.</summary>
        public double UnloadRatio, MaxSink;

        /// <summary>Viscous damping per footprint area, N·s/m³.</summary>
        public double Damping;

        /// <summary>μs and μk of the drone's polymer and carbon parts on the surface.</summary>
        public double StaticFriction, KineticFriction;

        /// <summary>The wet soil's normal adhesion σ_a and interface adhesion c_a (Pa), and the separation δ_a (m) at which σ_a lets go.</summary>
        public double Adhesion, InterfaceAdhesion, AdhesionRelease;

        /// <summary>The radius the fiber bends over at a groove's lip, m (p7d-tether).</summary>
        public double LipEdgeRadius;
    }

    /// <summary>A ground surface by name (MapWorld.SurfaceIds) with its three moisture sets.</summary>
    public struct SurfaceRow
    {
        public string Name;
        public SoilSet Dry, Wet, Saturated;

        public readonly SoilSet Set(Moisture m) => m == Moisture.Dry ? Dry : m == Moisture.Wet ? Wet : Saturated;
    }

    /// <summary>
    /// One moisture set of a cover kind: what a drawn straw, stem, twig or leaf is made of. A range's bounds hold where the
    /// element's own value is drawn by its Id's hash (world-derived, never a flight draw); a field a kind lacks is 0.
    /// </summary>
    public struct CoverSet
    {
        /// <summary>The tissue's Young's modulus, Pa; 0 for a leaf.</summary>
        public double ModulusMin, ModulusMax;

        /// <summary>The hollow wall's thickness over the diameter (0.5 solid), and the wall tissue's density, kg/m³; 0 for a leaf.</summary>
        public double WallShare, Density;

        /// <summary>A leaf's dry mass per area (kg/m²), its dry-matter content and its fresh mass per area (kg/m²); 0 otherwise.</summary>
        public double DryMassPerArea, DryMatterContent, MassPerArea;

        /// <summary>A leaf's petiole, where its cut is taken, m; 0 otherwise.</summary>
        public double PetioleDiameter;

        /// <summary>The pull at which a hooked element lets go, N; 0 for a kind that never hooks.</summary>
        public double HookReleaseMin, HookReleaseMax;

        /// <summary>The energy per section area to cut through it, J/m².</summary>
        public double CutEnergy;

        /// <summary>μ across a loose element's axis as it rolls under a part (0 for a kind that never rolls), and ζ of its bending.</summary>
        public double RollFriction, DampingRatio;
    }

    /// <summary>A cover kind with its dry and wet sets.</summary>
    public struct CoverRow
    {
        public CoverKind Kind;
        public CoverSet Dry, Wet;

        public readonly CoverSet Set(Moisture m) => m == Moisture.Dry ? Dry : Wet;
    }

    /// <summary>The named factors and values the wet and saturated sets are derived by (surfaces.json's "wetting").</summary>
    public struct Wetting
    {
        public double BearingWet, BearingSaturated, UnloadRatioWet, UnloadRatioSaturated, MaxSinkWet, MaxSinkSaturated;
        public double FrictionWet, FrictionSaturated, AdhesionWet, AdhesionSaturated, InterfaceAdhesionWet, InterfaceAdhesionSaturated;
        public double AdhesionReleaseWet, AdhesionReleaseSaturated;
        public double CoverHookReleaseWet, StrawModulusWet, StrawCutEnergyWet, StrawDensityWet, LeafWaterFilm;
    }

    /// <summary>
    /// assets-src/surfaces.json, physics' ground surfaces table (p7a-ground design 1): the thirteen surfaces S01–S13 in
    /// MapWorld.SurfaceIds' order, each dry, wet and saturated, and the four cover kinds, each dry and wet; every value
    /// labelled, every value labelled derived meeting its formula. The file lies outside the world's content hash, so the
    /// log header records the table and its hash, and a replay refuses a log flown with another file. Read by DetJson
    /// (the same bits on every runtime); load time only.
    /// </summary>
    public sealed class SurfaceTable
    {
        public const string RelativePath = "assets-src/surfaces.json";

        /// <summary>surfaces.json's cover kinds, in CoverKind's order.</summary>
        public static readonly string[] CoverKinds = { "straw", "stem", "twig", "leaf" };

        /// <summary>Bekker's width b at which Bearing is stated, m (the reference foot's: a 15 mm disc's radius).</summary>
        public double ReferenceWidth;

        /// <summary>The firm layer's modulus past MaxSink, N/m³.</summary>
        public double FirmLayer;

        public Wetting Wetting;

        /// <summary>The surfaces in MapWorld.SurfaceIds' order: surface id i + 1 is Surfaces[i].</summary>
        public SurfaceRow[] Surfaces = new SurfaceRow[0];

        /// <summary>The cover kinds in CoverKind's order.</summary>
        public CoverRow[] Cover = new CoverRow[0];

        /// <summary>The file's identity (DroneSpec.HashText's rule), which the log records and a replay checks.</summary>
        public ulong Hash;

        /// <summary>The set a rain level reads (D-021: Sunny means dry).</summary>
        public static Moisture MoistureOf(RainLevel rain) =>
            rain == RainLevel.Sunny ? Moisture.Dry : rain == RainLevel.Rain ? Moisture.Wet : Moisture.Saturated;

        public SurfaceTable Clone()
        {
            var t = (SurfaceTable)MemberwiseClone();
            t.Surfaces = (SurfaceRow[])Surfaces.Clone();
            t.Cover = (CoverRow[])Cover.Clone();
            return t;
        }

        static readonly string[] SoilFields =
        {
            "bearing_n_per_m3", "cohesive_share", "k_c_n_per_m2", "k_phi_n_per_m3", "unload_ratio", "max_sink_m", "damping_ns_per_m3",
            "friction_static", "friction_kinetic", "adhesion_pa", "interface_adhesion_pa", "adhesion_release_m", "lip_edge_radius_m",
        };

        static readonly string[] RigidFields = { "friction_static", "friction_kinetic" };

        // Each kind's fields, in the file's order.
        static readonly string[][] CoverFields =
        {
            new[] { "elastic_modulus_pa", "wall_share", "density_kg_m3", "hook_release_n", "cut_energy_j_per_m2", "roll_friction", "damping_ratio" },
            new[] { "elastic_modulus_pa", "wall_share", "density_kg_m3", "hook_release_n", "cut_energy_j_per_m2", "damping_ratio" },
            new[] { "elastic_modulus_pa", "wall_share", "density_kg_m3", "hook_release_n", "cut_energy_j_per_m2", "roll_friction", "damping_ratio" },
            new[] { "dry_mass_per_area_kg_m2", "dry_matter_content", "mass_per_area_kg_m2", "petiole_diameter_m", "cut_energy_j_per_m2" },
        };

        static readonly string[] Levels = { "dry", "wet", "saturated" };

        /// <summary>The table the file holds, with its hash. Throws FormatException naming every problem (path, field, rule).</summary>
        public static SurfaceTable Parse(string json)
        {
            JsonNode root = DetJson.Parse(json);
            var r = new LabelledReader();
            var t = new SurfaceTable();
            if (!r.IsObject(root, "surfaces.json")) throw Invalid(r);
            r.Members(root, "surfaces.json", "schema", "owner", "about", "fields", "constants", "wetting", "surfaces", "cover");
            JsonNode schema = root.Member("schema");
            if (schema == null || schema.Kind != JsonKind.Number || schema.Number != 1) r.Fail("surfaces.json.schema", "must be 1");

            JsonNode constants = root.Member("constants");
            if (r.IsObject(constants, "constants"))
            {
                r.Members(constants, "constants", "reference_width_m", "firm_layer_n_per_m3");
                t.ReferenceWidth = r.Number(constants, "reference_width_m", "constants", out _, 0, true);
                t.FirmLayer = r.Number(constants, "firm_layer_n_per_m3", "constants", out _, 0, true);
            }
            t.Wetting = ReadWetting(root.Member("wetting"), r);

            JsonNode surfaces = root.Member("surfaces");
            if (r.IsObject(surfaces, "surfaces"))
            {
                string[] ids = MapWorld.SurfaceIds;
                if (!Same(surfaces.Keys, ids))
                    r.Fail("surfaces", "must hold the thirteen surfaces S01–S13 in MapWorld.SurfaceIds' order (" + string.Join(", ", ids)
                        + "), not (" + string.Join(", ", surfaces.Keys) + ")");
                t.Surfaces = new SurfaceRow[surfaces.Keys.Count];
                for (int i = 0; i < surfaces.Keys.Count; i++)
                    t.Surfaces[i] = ReadSurface(surfaces.Keys[i], surfaces.Items[i], t, r);
            }

            JsonNode cover = root.Member("cover");
            if (r.IsObject(cover, "cover"))
            {
                if (!Same(cover.Keys, CoverKinds))
                    r.Fail("cover", "must hold the cover kinds " + string.Join(", ", CoverKinds) + " in that order, not (" + string.Join(", ", cover.Keys) + ")");
                else
                {
                    t.Cover = new CoverRow[CoverKinds.Length];
                    for (int k = 0; k < CoverKinds.Length; k++)
                        t.Cover[k] = ReadCover((CoverKind)k, cover.Items[k], t.Wetting, r);
                }
            }
            if (r.Problems.Count > 0) throw Invalid(r);
            t.Hash = DroneSpec.HashText(json);
            return t;
        }

        static FormatException Invalid(LabelledReader r) =>
            new FormatException("surfaces.json is not valid:\n  " + string.Join("\n  ", r.Problems));

        static bool Same(System.Collections.Generic.List<string> keys, string[] expected)
        {
            if (keys.Count != expected.Length) return false;
            for (int i = 0; i < expected.Length; i++)
                if (!string.Equals(keys[i], expected[i], StringComparison.Ordinal)) return false;
            return true;
        }

        static Wetting ReadWetting(JsonNode o, LabelledReader r)
        {
            var w = new Wetting();
            const string p = "wetting";
            if (!r.IsObject(o, p)) return w;
            r.Members(o, p, "bearing_wet", "bearing_saturated", "unload_ratio_wet", "unload_ratio_saturated", "max_sink_wet", "max_sink_saturated",
                "friction_wet", "friction_saturated", "adhesion_wet_pa", "adhesion_saturated_pa", "interface_adhesion_wet_pa",
                "interface_adhesion_saturated_pa", "adhesion_release_wet_m", "adhesion_release_saturated_m", "cover_hook_release_wet",
                "straw_modulus_wet", "straw_cut_energy_wet", "straw_density_wet", "leaf_water_film_kg_m2");
            w.BearingWet = r.Number(o, "bearing_wet", p, out _, 0, true, 1);
            w.BearingSaturated = r.Number(o, "bearing_saturated", p, out _, 0, true, 1);
            w.UnloadRatioWet = r.Number(o, "unload_ratio_wet", p, out _, 1);
            w.UnloadRatioSaturated = r.Number(o, "unload_ratio_saturated", p, out _, 1);
            w.MaxSinkWet = r.Number(o, "max_sink_wet", p, out _, 0, true);
            w.MaxSinkSaturated = r.Number(o, "max_sink_saturated", p, out _, 0, true);
            w.FrictionWet = r.Number(o, "friction_wet", p, out _, 0, true);
            w.FrictionSaturated = r.Number(o, "friction_saturated", p, out _, 0, true);
            w.AdhesionWet = r.Number(o, "adhesion_wet_pa", p, out _);
            w.AdhesionSaturated = r.Number(o, "adhesion_saturated_pa", p, out _);
            w.InterfaceAdhesionWet = r.Number(o, "interface_adhesion_wet_pa", p, out _);
            w.InterfaceAdhesionSaturated = r.Number(o, "interface_adhesion_saturated_pa", p, out _);
            w.AdhesionReleaseWet = r.Number(o, "adhesion_release_wet_m", p, out _, 0, true);
            w.AdhesionReleaseSaturated = r.Number(o, "adhesion_release_saturated_m", p, out _, 0, true);
            w.CoverHookReleaseWet = r.Number(o, "cover_hook_release_wet", p, out _, 0, true);
            w.StrawModulusWet = r.Number(o, "straw_modulus_wet", p, out _, 0, true);
            w.StrawCutEnergyWet = r.Number(o, "straw_cut_energy_wet", p, out _, 0, true);
            w.StrawDensityWet = r.Number(o, "straw_density_wet", p, out _, 0, true);
            w.LeafWaterFilm = r.Number(o, "leaf_water_film_kg_m2", p, out _);
            return w;
        }

        static SurfaceRow ReadSurface(string name, JsonNode o, SurfaceTable t, LabelledReader r)
        {
            var row = new SurfaceRow { Name = name };
            string path = "surfaces." + name;
            if (!r.IsObject(o, path)) return row;
            r.Members(o, path, "about", "dry", "wet", "saturated");
            JsonNode dry = o.Member("dry");
            bool rigid = dry != null && dry.Kind == JsonKind.Object && dry.Member("bearing_n_per_m3") == null;
            row.Dry = ReadSoil(dry, path + ".dry", rigid, Moisture.Dry, default, t, r);
            row.Wet = ReadSoil(o.Member("wet"), path + ".wet", rigid, Moisture.Wet, row.Dry, t, r);
            row.Saturated = ReadSoil(o.Member("saturated"), path + ".saturated", rigid, Moisture.Saturated, row.Dry, t, r);
            return row;
        }

        // A moisture set; a wet or saturated one checks each value labelled derived against wetting's formula from the dry set.
        static SoilSet ReadSoil(JsonNode o, string path, bool rigid, Moisture m, in SoilSet dry, SurfaceTable t, LabelledReader r)
        {
            var s = new SoilSet { Rigid = rigid };
            if (!r.IsObject(o, path)) return s;
            r.Members(o, path, rigid ? RigidFields : SoilFields);
            bool wet = m != Moisture.Dry;
            Wetting w = t.Wetting;
            double fFriction = m == Moisture.Wet ? w.FrictionWet : w.FrictionSaturated;
            string level = Levels[(int)m];

            s.StaticFriction = r.Number(o, "friction_static", path, out string lmus, 0, true);
            s.KineticFriction = r.Number(o, "friction_kinetic", path, out string lmuk, 0, true);
            if (s.KineticFriction > s.StaticFriction) r.Fail(path + ".friction_kinetic", "is above friction_static");
            if (wet)
            {
                r.Derived(lmus, s.StaticFriction, dry.StaticFriction * fFriction, path + ".friction_static", "the dry set's × wetting.friction_" + level);
                r.Derived(lmuk, s.KineticFriction, dry.KineticFriction * fFriction, path + ".friction_kinetic", "the dry set's × wetting.friction_" + level);
            }
            if (rigid) return s;

            s.Bearing = r.Number(o, "bearing_n_per_m3", path, out string lb, 0, true);
            s.CohesiveShare = r.Number(o, "cohesive_share", path, out string lsh, 0, false, 1);
            s.CohesiveModulus = r.Number(o, "k_c_n_per_m2", path, out string lkc);
            s.FrictionalModulus = r.Number(o, "k_phi_n_per_m3", path, out string lkp);
            s.UnloadRatio = r.Number(o, "unload_ratio", path, out string lu, 1);
            s.MaxSink = r.Number(o, "max_sink_m", path, out string lms, 0, true);
            s.Damping = r.Number(o, "damping_ns_per_m3", path, out string ld);
            s.Adhesion = r.Number(o, "adhesion_pa", path, out string la);
            s.InterfaceAdhesion = r.Number(o, "interface_adhesion_pa", path, out string lia);
            s.AdhesionRelease = r.Number(o, "adhesion_release_m", path, out string lar);
            s.LipEdgeRadius = r.Number(o, "lip_edge_radius_m", path, out string llip, 0, true);
            if (s.Adhesion > 0 && !(s.AdhesionRelease > 0)) r.Fail(path + ".adhesion_release_m", "must be above 0 where adhesion_pa is");

            r.Derived(lkc, s.CohesiveModulus, s.CohesiveShare * s.Bearing * t.ReferenceWidth, path + ".k_c_n_per_m2",
                "cohesive_share × bearing_n_per_m3 × reference_width_m");
            r.Derived(lkp, s.FrictionalModulus, (1 - s.CohesiveShare) * s.Bearing, path + ".k_phi_n_per_m3", "(1 − cohesive_share) × bearing_n_per_m3");
            if (!wet) return s;

            bool sat = m == Moisture.Saturated;
            r.Derived(lb, s.Bearing, dry.Bearing * (sat ? w.BearingSaturated : w.BearingWet), path + ".bearing_n_per_m3", "the dry set's × wetting.bearing_" + level);
            r.Derived(lsh, s.CohesiveShare, dry.CohesiveShare, path + ".cohesive_share", "the dry set's");
            r.Derived(lu, s.UnloadRatio, dry.UnloadRatio * (sat ? w.UnloadRatioSaturated : w.UnloadRatioWet), path + ".unload_ratio",
                "the dry set's × wetting.unload_ratio_" + level);
            r.Derived(lms, s.MaxSink, dry.MaxSink * (sat ? w.MaxSinkSaturated : w.MaxSinkWet), path + ".max_sink_m", "the dry set's × wetting.max_sink_" + level);
            r.Derived(ld, s.Damping, dry.Damping, path + ".damping_ns_per_m3", "the dry set's");
            r.Derived(la, s.Adhesion, sat ? w.AdhesionSaturated : w.AdhesionWet, path + ".adhesion_pa", "wetting.adhesion_" + level + "_pa");
            r.Derived(lia, s.InterfaceAdhesion, sat ? w.InterfaceAdhesionSaturated : w.InterfaceAdhesionWet, path + ".interface_adhesion_pa",
                "wetting.interface_adhesion_" + level + "_pa");
            r.Derived(lar, s.AdhesionRelease, sat ? w.AdhesionReleaseSaturated : w.AdhesionReleaseWet, path + ".adhesion_release_m",
                "wetting.adhesion_release_" + level + "_m");
            r.Derived(llip, s.LipEdgeRadius, dry.LipEdgeRadius, path + ".lip_edge_radius_m", "the dry set's");
            return s;
        }

        static CoverRow ReadCover(CoverKind kind, JsonNode o, in Wetting w, LabelledReader r)
        {
            var row = new CoverRow { Kind = kind };
            string path = "cover." + CoverKinds[(int)kind];
            if (!r.IsObject(o, path)) return row;
            r.Members(o, path, "dry", "wet");
            row.Dry = ReadCoverSet(kind, o.Member("dry"), path + ".dry", false, default, w, r);
            row.Wet = ReadCoverSet(kind, o.Member("wet"), path + ".wet", true, row.Dry, w, r);
            return row;
        }

        static CoverSet ReadCoverSet(CoverKind kind, JsonNode o, string path, bool wet, in CoverSet dry, in Wetting w, LabelledReader r)
        {
            var c = new CoverSet();
            if (!r.IsObject(o, path)) return c;
            string[] fields = CoverFields[(int)kind];
            r.Members(o, path, fields);
            bool straw = kind == CoverKind.Straw;
            if (kind == CoverKind.Leaf)
            {
                c.DryMassPerArea = r.Number(o, "dry_mass_per_area_kg_m2", path, out string ldm, 0, true);
                c.DryMatterContent = r.Number(o, "dry_matter_content", path, out string ldc, 0, true, 1);
                c.MassPerArea = r.Number(o, "mass_per_area_kg_m2", path, out string lma, 0, true);
                c.PetioleDiameter = r.Number(o, "petiole_diameter_m", path, out string lpd, 0, true);
                c.CutEnergy = r.Number(o, "cut_energy_j_per_m2", path, out string lce, 0, true);
                if (!wet)
                {
                    r.Derived(lma, c.MassPerArea, c.DryMassPerArea / c.DryMatterContent, path + ".mass_per_area_kg_m2",
                        "dry_mass_per_area_kg_m2 / dry_matter_content");
                    return c;
                }
                r.Derived(ldm, c.DryMassPerArea, dry.DryMassPerArea, path + ".dry_mass_per_area_kg_m2", "the dry set's");
                r.Derived(ldc, c.DryMatterContent, dry.DryMatterContent, path + ".dry_matter_content", "the dry set's");
                r.Derived(lma, c.MassPerArea, dry.MassPerArea + w.LeafWaterFilm, path + ".mass_per_area_kg_m2", "the dry set's + wetting.leaf_water_film_kg_m2");
                r.Derived(lpd, c.PetioleDiameter, dry.PetioleDiameter, path + ".petiole_diameter_m", "the dry set's");
                r.Derived(lce, c.CutEnergy, dry.CutEnergy, path + ".cut_energy_j_per_m2", "the dry set's");
                return c;
            }
            r.Range(o, "elastic_modulus_pa", path, out c.ModulusMin, out c.ModulusMax, out string le);
            c.WallShare = r.Number(o, "wall_share", path, out string lw, 0, true, 0.5);
            c.Density = r.Number(o, "density_kg_m3", path, out string lden, 0, true);
            r.Range(o, "hook_release_n", path, out c.HookReleaseMin, out c.HookReleaseMax, out string lh);
            c.CutEnergy = r.Number(o, "cut_energy_j_per_m2", path, out string lc, 0, true);
            string lr = null;
            if (Array.IndexOf(fields, "roll_friction") >= 0) c.RollFriction = r.Number(o, "roll_friction", path, out lr, 0, true);
            c.DampingRatio = r.Number(o, "damping_ratio", path, out string lz);
            if (!wet) return c;

            double fE = straw ? w.StrawModulusWet : 1, fC = straw ? w.StrawCutEnergyWet : 1, fD = straw ? w.StrawDensityWet : 1;
            string sE = straw ? "the dry set's × wetting.straw_modulus_wet" : "the dry set's";
            r.Derived(le, c.ModulusMin, dry.ModulusMin * fE, path + ".elastic_modulus_pa[0]", sE);
            r.Derived(le, c.ModulusMax, dry.ModulusMax * fE, path + ".elastic_modulus_pa[1]", sE);
            r.Derived(lw, c.WallShare, dry.WallShare, path + ".wall_share", "the dry set's");
            r.Derived(lden, c.Density, dry.Density * fD, path + ".density_kg_m3", straw ? "the dry set's × wetting.straw_density_wet" : "the dry set's");
            r.Derived(lh, c.HookReleaseMin, dry.HookReleaseMin * w.CoverHookReleaseWet, path + ".hook_release_n[0]", "the dry set's × wetting.cover_hook_release_wet");
            r.Derived(lh, c.HookReleaseMax, dry.HookReleaseMax * w.CoverHookReleaseWet, path + ".hook_release_n[1]", "the dry set's × wetting.cover_hook_release_wet");
            r.Derived(lc, c.CutEnergy, dry.CutEnergy * fC, path + ".cut_energy_j_per_m2", straw ? "the dry set's × wetting.straw_cut_energy_wet" : "the dry set's");
            r.Derived(lr, c.RollFriction, dry.RollFriction, path + ".roll_friction", "the dry set's");
            r.Derived(lz, c.DampingRatio, dry.DampingRatio, path + ".damping_ratio", "the dry set's");
            return c;
        }
    }
}
