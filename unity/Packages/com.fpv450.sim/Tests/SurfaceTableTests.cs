using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // ground-contact (p7a-ground 1.1): physics' surfaces table, assets-src/surfaces.json, and physics_materials.json's p7a
    // fields: the loaders' refusals, every derived value against its formula (recomputed here, apart from the loader's
    // own check), the reference foot's sinks through Bekker's law, and the table in the log header with the replay's
    // refusal of another file. Nothing reads the table in a step yet, so a flight with it steps as one without.
    public sealed class SurfaceTableTests
    {
        static string _text;

        static string Text() => _text ??= File.ReadAllText(ContactScenarios.RepositoryFile(SurfaceTable.RelativePath));

        static SurfaceTable Table() => SurfaceTable.Parse(Text());

        static string MaterialsText() => File.ReadAllText(ContactScenarios.RepositoryFile(PhysicsMaterials.RelativePath));

        // The text with the first `old` after the anchors (found in turn, '|' between them) replaced.
        static string Edit(string json, string anchors, string old, string replacement)
        {
            int at = 0;
            foreach (string a in anchors.Split('|'))
            {
                at = json.IndexOf(a, at, StringComparison.Ordinal);
                Assert.That(at, Is.GreaterThanOrEqualTo(0), "anchor " + a);
                at += a.Length;
            }
            int i = json.IndexOf(old, at, StringComparison.Ordinal);
            Assert.That(i, Is.GreaterThanOrEqualTo(0), "'" + old + "' after " + anchors);
            return json.Substring(0, i) + replacement + json.Substring(i + old.Length);
        }

        static void Close(double value, double formula, string what)
        {
            if (formula == 0) Assert.That(value, Is.EqualTo(0), what);
            else Assert.That(value, Is.EqualTo(formula).Within(1e-9 * Math.Abs(formula)), what);
        }

        static SurfaceRow Surface(SurfaceTable t, string name) => Array.Find(t.Surfaces, s => s.Name == name);

        // ---- "Every row labelled and loaded" -----------------------------------------------------------------------------

        [Test]
        public void EveryRowIsLabelledAndLoaded()
        {
            SurfaceTable t = Table();
            Assert.That(t.Hash, Is.Not.EqualTo(0UL));
            Assert.That(t.Surfaces.Length, Is.EqualTo(13), "S01–S13");
            for (int i = 0; i < t.Surfaces.Length; i++)
            {
                SurfaceRow s = t.Surfaces[i];
                Assert.That(s.Name, Is.EqualTo(MapWorld.SurfaceIds[i]), "surface id " + (i + 1) + " is MapWorld's");
                bool rigid = s.Name == "asphalt" || s.Name == "concrete_slab";
                foreach (Moisture m in new[] { Moisture.Dry, Moisture.Wet, Moisture.Saturated })
                {
                    SoilSet set = s.Set(m);
                    string at = s.Name + " " + m;
                    Assert.That(set.Rigid, Is.EqualTo(rigid), at);
                    Assert.That(set.StaticFriction, Is.GreaterThanOrEqualTo(set.KineticFriction), at);
                    Assert.That(set.KineticFriction, Is.GreaterThan(0), at);
                    if (rigid) continue;
                    Assert.That(set.Bearing, Is.GreaterThan(0), at);
                    Assert.That(set.UnloadRatio, Is.GreaterThanOrEqualTo(1), at);
                    Assert.That(set.MaxSink, Is.GreaterThan(0), at);
                    Assert.That(set.LipEdgeRadius, Is.GreaterThan(0), at);
                    if (m == Moisture.Dry) Assert.That(set.Adhesion + set.InterfaceAdhesion, Is.EqualTo(0), at + ": dry soil holds no film");
                }
            }
            Assert.That(t.Cover.Length, Is.EqualTo(4), "straw, stem, twig, leaf");
            for (int k = 0; k < t.Cover.Length; k++)
            {
                CoverRow c = t.Cover[k];
                Assert.That(c.Kind, Is.EqualTo((CoverKind)k));
                foreach (CoverSet set in new[] { c.Dry, c.Wet })
                {
                    Assert.That(set.CutEnergy, Is.GreaterThan(0), c.Kind.ToString());
                    if (c.Kind == CoverKind.Leaf) Assert.That(set.MassPerArea, Is.GreaterThan(0));
                    else Assert.That(set.ModulusMin > 0 && set.ModulusMin <= set.ModulusMax && set.HookReleaseMin > 0 && set.Density > 0, Is.True, c.Kind.ToString());
                }
                Assert.That(c.Set(Moisture.Saturated), Is.EqualTo(c.Wet), "a cover kind reads wet in both rains");
            }
            Assert.That(SurfaceTable.MoistureOf(RainLevel.Sunny), Is.EqualTo(Moisture.Dry), "D-021: Sunny means dry");
            Assert.That(SurfaceTable.MoistureOf(RainLevel.Rain), Is.EqualTo(Moisture.Wet));
            Assert.That(SurfaceTable.MoistureOf(RainLevel.SevereRain), Is.EqualTo(Moisture.Saturated));

            // One value's label removed: refused, naming the surface, the field and the missing label.
            string noLabel = Edit(Text(), "\"belt_straw\"|\"wet\"|\"bearing_n_per_m3\"", "\"label\": \"derived\", ", "");
            string m1 = Assert.Throws<FormatException>(() => SurfaceTable.Parse(noLabel)).Message;
            TestContext.Out.WriteLine(m1);
            StringAssert.Contains("surfaces.belt_straw.wet.bearing_n_per_m3.label: missing", m1);
            string noDryLabel = Edit(Text(), "\"meadow_sod\"|\"dry\"|\"friction_static\"", "\"label\": \"assumed\", ", "");
            StringAssert.Contains("surfaces.meadow_sod.dry.friction_static.label: missing",
                Assert.Throws<FormatException>(() => SurfaceTable.Parse(noDryLabel)).Message);
        }

        // ---- the loader refuses bad files --------------------------------------------------------------------------------

        [TestCase("\"belt_straw\"|\"wet\"|\"bearing_n_per_m3\"", "1.41e6", "1.5e6", "surfaces.belt_straw.wet.bearing_n_per_m3: is labelled derived")]
        [TestCase("\"meadow_sod\"|\"dry\"|\"k_c_n_per_m2\"", "26250.0", "26000.0", "surfaces.meadow_sod.dry.k_c_n_per_m2: is labelled derived")]
        [TestCase("\"meadow_sod\"|\"dry\"|\"k_phi_n_per_m3\"", "3.5e6", "3.4e6", "surfaces.meadow_sod.dry.k_phi_n_per_m3: is labelled derived")]
        [TestCase("\"crater_spoil\"|\"saturated\"|\"friction_static\"", "0.48", "0.5", "surfaces.crater_spoil.saturated.friction_static: is labelled derived")]
        [TestCase("\"tilled\"|\"wet\"|\"unload_ratio\"", "12.0", "8.0", "surfaces.tilled.wet.unload_ratio: is labelled derived")]
        [TestCase("\"tilled\"|\"wet\"|\"adhesion_pa\"", "3000.0", "2500.0", "surfaces.tilled.wet.adhesion_pa: is labelled derived")]
        [TestCase("\"cover\"|\"straw\"|\"wet\"|\"elastic_modulus_pa\"", "3.36e9", "3.4e9", "cover.straw.wet.elastic_modulus_pa[0]: is labelled derived")]
        [TestCase("\"cover\"|\"leaf\"|\"dry\"|\"mass_per_area_kg_m2\"", "0.2,", "0.25,", "cover.leaf.dry.mass_per_area_kg_m2: is labelled derived")]
        [TestCase("\"cover\"|\"twig\"|\"wet\"|\"hook_release_n\"", "0.45", "0.3", "cover.twig.wet.hook_release_n[0]: is labelled derived")]
        [TestCase("\"rubble\"|\"dry\"", "\"unload_ratio\": {", "\"porosity\": {\"value\": 0.4, \"label\": \"assumed\", \"note\": \"x\"}, \"unload_ratio\": {",
            "surfaces.rubble.dry.porosity: is not a field of the schema")]
        [TestCase("\"surfaces\": {", "\"weeds\": {", "\"weed\": {", "surfaces: must hold the thirteen surfaces S01–S13")]
        [TestCase("\"belt_bare\"|\"dry\"|\"adhesion_release_m\"", ", \"note\": \"dry: no adhesion to release\"", "", "surfaces.belt_bare.dry.adhesion_release_m.note: missing")]
        [TestCase("\"dry_crust\"|\"dry\"|\"unload_ratio\"", "1.5", "0.5", "surfaces.dry_crust.dry.unload_ratio: must be at least 1")]
        [TestCase("\"asphalt\"|\"dry\"|\"friction_kinetic\"", "0.4", "0.55", "surfaces.asphalt.dry.friction_kinetic: is above friction_static")]
        [TestCase("\"concrete_slab\"|\"wet\"", "\"friction_static\"", "\"bearing_n_per_m3\": {\"value\": 1e9, \"label\": \"assumed\", \"note\": \"x\"}, \"friction_static\"",
            "surfaces.concrete_slab.wet.bearing_n_per_m3: is not a field of the schema")]
        [TestCase("\"wetting\"|\"adhesion_release_wet_m\"", "\"label\": \"assumed\", ", "\"label\": \"guessed\", ", "wetting.adhesion_release_wet_m.label: must be one of")]
        public void TheLoaderRefusesABadFile(string anchors, string old, string replacement, string expected)
        {
            string bad = Edit(Text(), anchors, old, replacement);
            string message = Assert.Throws<FormatException>(() => SurfaceTable.Parse(bad)).Message;
            TestContext.Out.WriteLine(message);
            StringAssert.Contains(expected, message);
        }

        // ---- each derived value meets its formula ------------------------------------------------------------------------

        [Test]
        public void EachDerivedValueMeetsItsFormula()
        {
            SurfaceTable t = Table();
            Wetting w = t.Wetting;
            Assert.That(t.ReferenceWidth, Is.EqualTo(0.015 / 2), "Bekker's b of the 15 mm reference disc is its radius");
            int formulas = 0;
            foreach (SurfaceRow s in t.Surfaces)
            {
                SoilSet dry = s.Dry;
                foreach (Moisture m in new[] { Moisture.Dry, Moisture.Wet, Moisture.Saturated })
                {
                    SoilSet x = s.Set(m);
                    string at = s.Name + " " + m + " ";
                    if (!x.Rigid)
                    {
                        Close(x.CohesiveModulus, x.CohesiveShare * x.Bearing * t.ReferenceWidth, at + "k_c = share × bearing × b_ref");
                        Close(x.FrictionalModulus, (1 - x.CohesiveShare) * x.Bearing, at + "k_phi = (1 − share) × bearing");
                        formulas += 2;
                    }
                    if (m == Moisture.Dry) continue;
                    bool sat = m == Moisture.Saturated;
                    double fMu = sat ? w.FrictionSaturated : w.FrictionWet;
                    Close(x.StaticFriction, dry.StaticFriction * fMu, at + "μs");
                    Close(x.KineticFriction, dry.KineticFriction * fMu, at + "μk");
                    formulas += 2;
                    if (x.Rigid) continue;
                    Close(x.CohesiveShare, dry.CohesiveShare, at + "share");
                    Close(x.Damping, dry.Damping, at + "damping");
                    Close(x.LipEdgeRadius, dry.LipEdgeRadius, at + "lip");
                    formulas += 3;
                    if (s.Name == "rubble")
                    {
                        // The table's one labelled departure: granular debris drains, so it keeps its dry soil and holds no film.
                        Assert.That(x.Bearing == dry.Bearing && x.UnloadRatio == dry.UnloadRatio && x.MaxSink == dry.MaxSink, Is.True, at);
                        Assert.That(x.Adhesion == 0 && x.InterfaceAdhesion == 0 && x.AdhesionRelease == 0, Is.True, at);
                        continue;
                    }
                    Close(x.Bearing, dry.Bearing * (sat ? w.BearingSaturated : w.BearingWet), at + "bearing");
                    Close(x.UnloadRatio, dry.UnloadRatio * (sat ? w.UnloadRatioSaturated : w.UnloadRatioWet), at + "unload ratio");
                    Close(x.MaxSink, dry.MaxSink * (sat ? w.MaxSinkSaturated : w.MaxSinkWet), at + "max sink");
                    Close(x.Adhesion, sat ? w.AdhesionSaturated : w.AdhesionWet, at + "σ_a");
                    Close(x.InterfaceAdhesion, sat ? w.InterfaceAdhesionSaturated : w.InterfaceAdhesionWet, at + "c_a");
                    Close(x.AdhesionRelease, sat ? w.AdhesionReleaseSaturated : w.AdhesionReleaseWet, at + "δ_a");
                    formulas += 6;
                }
            }
            foreach (CoverRow c in t.Cover)
            {
                CoverSet dry = c.Dry, wet = c.Wet;
                string at = c.Kind + " wet ";
                if (c.Kind == CoverKind.Leaf)
                {
                    Close(dry.MassPerArea, dry.DryMassPerArea / dry.DryMatterContent, "leaf: fresh = dry mass per area / dry-matter content");
                    Close(wet.MassPerArea, dry.MassPerArea + w.LeafWaterFilm, "leaf: wet = fresh + film");
                    Close(wet.PetioleDiameter, dry.PetioleDiameter, at + "petiole");
                    Close(wet.CutEnergy, dry.CutEnergy, at + "cut energy");
                    formulas += 4;
                    continue;
                }
                bool straw = c.Kind == CoverKind.Straw;
                Close(wet.ModulusMin, dry.ModulusMin * (straw ? w.StrawModulusWet : 1), at + "E min");
                Close(wet.ModulusMax, dry.ModulusMax * (straw ? w.StrawModulusWet : 1), at + "E max");
                Close(wet.CutEnergy, dry.CutEnergy * (straw ? w.StrawCutEnergyWet : 1), at + "cut energy");
                Close(wet.Density, dry.Density * (straw ? w.StrawDensityWet : 1), at + "density");
                Close(wet.HookReleaseMin, dry.HookReleaseMin * w.CoverHookReleaseWet, at + "hook min");
                Close(wet.HookReleaseMax, dry.HookReleaseMax * w.CoverHookReleaseWet, at + "hook max");
                Close(wet.WallShare, dry.WallShare, at + "wall");
                Close(wet.RollFriction, dry.RollFriction, at + "roll");
                Close(wet.DampingRatio, dry.DampingRatio, at + "ζ");
                formulas += 9;
            }
            TestContext.Out.WriteLine(formulas + " formulas recomputed over the 13 surfaces and 4 cover kinds");
        }

        // ---- "The reference foot's sinks are kept" -----------------------------------------------------------------------

        // A flat disc of radius a on a soil set at sink z: Bekker's linear law with b the disc's radius, over its area, and
        // the firm layer past the maximum sink.
        static double DiscForce(in SoilSet s, SurfaceTable t, double a, double z)
        {
            double area = Math.PI * a * a;
            double f = (s.CohesiveModulus / a + s.FrictionalModulus) * area * z;
            return z > s.MaxSink ? f + t.FirmLayer * area * (z - s.MaxSink) : f;
        }

        [Test]
        public void TheReferenceFootsSinksAreKept()
        {
            SurfaceTable t = Table();
            const double load = 25, a = 0.0075;
            var k72 = new[]
            {
                ("meadow_sod", 0.020), ("dry_crust", 0.0028), ("crater_spoil", 0.051), ("belt_straw", 0.030), ("belt_bare", 0.010), ("tilled", 0.035),
                ("yard_litter", 0.0040), ("rubble", 0.0014), ("weeds", 0.010),
            };
            foreach ((string name, double sink72) in k72)
            {
                SoilSet s = Surface(t, name).Dry;
                double lo = 0, hi = 1;
                for (int i = 0; i < 200; i++)
                {
                    double mid = 0.5 * (lo + hi);
                    if (DiscForce(s, t, a, mid) < load) lo = mid;
                    else hi = mid;
                }
                double z = 0.5 * (lo + hi), fromRow = load / (s.Bearing * Math.PI * a * a);
                TestContext.Out.WriteLine($"{name}: the 15 mm disc at 25 N sinks {z * 1e3:F2} mm through Bekker's law, {fromRow * 1e3:F2} mm from the row's bearing, K§7.2 {sink72 * 1e3:F1} mm");
                Assert.That(z, Is.EqualTo(fromRow).Within(0.01 * fromRow), name + ": the law's sink is the row's");
                Assert.That(z, Is.EqualTo(sink72).Within(0.10 * sink72), name + ": K§7.2's sink is kept");
                Assert.That(z, Is.LessThan(s.MaxSink), name + ": it rests above the firm layer");
            }
        }

        // ---- the table in the log, "A replay refuses another table" ------------------------------------------------------

        // A disarmed rest on the floor, the legs on, with or without the table; the hash of its records and events.
        static byte[] Rest(SurfaceTable table, out ulong steps)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong mh);
            FlightSetup setup = ContactScenarios.Setup(spec, m, mh, true, new Double3(0, ContactScenarios.Standing(spec.BuildContacts(spec.Nominal, m, mh)), 0));
            setup.Contacts = spec.BuildContacts(spec.Nominal, m, mh, table);
            var core = new FlightCore(setup);
            LogHeader header = LogHeader.For(setup, core);
            header.WorldId = "floor";
            header.SetDrone(spec, spec.Nominal);
            var stream = new MemoryStream();
            var recorder = new FlightRecorder(stream, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            var record = new byte[LogFormat.StepSize];
            var e = new byte[LogFormat.EventSize];
            steps = 0xCBF29CE484222325UL;
            for (int k = 0; k < 400; k++)
            {
                var stick = new StickSample { HostUs = k * FlightCore.StepUs };
                core.Advance(stick);
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
                LogFormat.EncodeStep(record, core.State, stick, core.Trace);
                foreach (byte b in record) steps = (steps ^ b) * 0x100000001B3UL;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    LogFormat.EncodeEvent(e, core.State.Step, core.Events[i]);
                    foreach (byte b in e) steps = (steps ^ b) * 0x100000001B3UL;
                }
            }
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            return stream.ToArray();
        }

        static FlightWorld OpenWorld(string id) => id == "floor" ? FlightWorld.Floor() : null;

        [Test]
        public void AReplayRefusesAnotherTable()
        {
            SurfaceTable table = Table();
            byte[] log = Rest(table, out ulong with);
            Rest(null, out ulong without);
            // 1.10: the table's soil carries the skids (the floor's belt_bare), so the flight moves; its replay matches below.
            Assert.That(with, Is.Not.EqualTo(without), "the table's soil acts in a step");

            LogHeader h = new FlightLogReader(new MemoryStream(log)).Header;
            SurfaceTable logged = h.Contacts.Surfaces;
            Assert.That(logged, Is.Not.Null);
            Assert.That(logged.Hash, Is.EqualTo(table.Hash));
            Assert.That(logged.ReferenceWidth, Is.EqualTo(table.ReferenceWidth));
            Assert.That(logged.FirmLayer, Is.EqualTo(table.FirmLayer));
            Assert.That(logged.Wetting, Is.EqualTo(table.Wetting));
            Assert.That(logged.Surfaces.Length, Is.EqualTo(table.Surfaces.Length));
            for (int i = 0; i < table.Surfaces.Length; i++)
            {
                Assert.That(logged.Surfaces[i].Name, Is.EqualTo(table.Surfaces[i].Name));
                Assert.That(logged.Surfaces[i].Dry, Is.EqualTo(table.Surfaces[i].Dry), table.Surfaces[i].Name);
                Assert.That(logged.Surfaces[i].Wet, Is.EqualTo(table.Surfaces[i].Wet), table.Surfaces[i].Name);
                Assert.That(logged.Surfaces[i].Saturated, Is.EqualTo(table.Surfaces[i].Saturated), table.Surfaces[i].Name);
            }
            for (int k = 0; k < table.Cover.Length; k++) Assert.That(logged.Cover[k], Is.EqualTo(table.Cover[k]));
            MaterialRow straw = Array.Find(h.Contacts.Materials, r => r.Name == "straw");
            Assert.That(straw.AreaLaw == AreaLaw.Mat && straw.HasWet && straw.WetStiffness == 294, Is.True, "the materials' p7a fields are logged");
            var summary = new StringWriter();
            FlightSummary.Read(new MemoryStream(log)).Print(summary);
            StringAssert.Contains("surfaces.json 0x" + table.Hash.ToString("X16"), summary.ToString());

            ulong drone = RepositoryDrone.Spec().ContentHash;
            ReplayResult same = FlightReplay.Run(new MemoryStream(log), OpenWorld, drone, null, table.Hash);
            Assert.That(same.Outcome, Is.EqualTo(ReplayOutcome.Match), same.Message);

            // One value changed (rubble's wet unload ratio, a labelled departure nothing derives from): refused before its first step.
            ulong other = SurfaceTable.Parse(Edit(Text(), "\"rubble\"|\"wet\"|\"unload_ratio\"", "\"value\": 1.5,", "\"value\": 1.6,")).Hash;
            Assert.That(other, Is.Not.EqualTo(table.Hash));
            ReplayResult refused = FlightReplay.Run(new MemoryStream(log), OpenWorld, drone, null, other);
            TestContext.Out.WriteLine(refused.Message);
            Assert.That(refused.Outcome, Is.EqualTo(ReplayOutcome.Refused));
            Assert.That(refused.Steps, Is.EqualTo(0));
            StringAssert.Contains("surfaces.json differs", refused.Message);
            StringAssert.Contains("0x" + table.Hash.ToString("X16"), refused.Message);
            StringAssert.Contains("0x" + other.ToString("X16"), refused.Message);
            Assert.That(FlightReplay.Run(new MemoryStream(log), OpenWorld, drone).Outcome, Is.EqualTo(ReplayOutcome.Refused), "a replay without the file");

            byte[] plain = Rest(null, out _);
            Assert.That(FlightReplay.Run(new MemoryStream(plain), OpenWorld, drone, null, other).Outcome, Is.EqualTo(ReplayOutcome.Match),
                "a log without a table replays whatever file the replay holds");
        }

        [Test]
        public void TheHeaderCoversTheSurfaceTypes()
        {
            Assert.That(Fields(typeof(SurfaceTable)), Is.EquivalentTo(LogHeader.SurfaceTableFieldsLogged));
            Assert.That(Fields(typeof(SurfaceRow)), Is.EquivalentTo(LogHeader.SurfaceRowFieldsLogged));
            Assert.That(Fields(typeof(SoilSet)), Is.EquivalentTo(LogHeader.SoilSetFieldsLogged));
            Assert.That(Fields(typeof(CoverRow)), Is.EquivalentTo(LogHeader.CoverRowFieldsLogged));
            Assert.That(Fields(typeof(CoverSet)), Is.EquivalentTo(LogHeader.CoverSetFieldsLogged));
            Assert.That(Fields(typeof(Wetting)), Is.EquivalentTo(LogHeader.WettingFieldsLogged));
        }

        static string[] Fields(Type t)
        {
            var names = new List<string>();
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) names.Add(f.Name);
            return names.ToArray();
        }

        // ---- physics_materials.json's p7a fields: "An area-type row says its law" ----------------------------------------

        [Test]
        public void AnAreaRowSaysItsLaw()
        {
            MaterialRow[] rows = PhysicsMaterials.Parse(MaterialsText(), out _);
            MaterialRow Row(string n) => Array.Find(rows, r => r.Name == n);
            Assert.That(Row("straw").AreaLaw, Is.EqualTo(AreaLaw.Mat));
            Assert.That(Row("straw").AreaReferenceDepth, Is.EqualTo(0.125), "S04's mat");
            Assert.That(Row("fabric").AreaLaw, Is.EqualTo(AreaLaw.Punch));
            Assert.That(Row("sandbag").AreaLaw, Is.EqualTo(AreaLaw.Winkler));
            foreach (MaterialRow r in rows)
                if (Array.IndexOf(PhysicsMaterials.AreaRows, r.Name) < 0)
                    Assert.That(r.AreaLaw, Is.EqualTo(AreaLaw.None), r.Name);
            // The mat law's modulus for the reference foot: E_m = k·D/A_s, A_s the 2:1 spread disc at D (the row's 3.98 kPa).
            double spread = Math.PI * Math.Pow((0.015 + 0.125) / 2, 2);
            Assert.That(Row("straw").Stiffness * 0.125 / spread, Is.EqualTo(3979).Within(1));
            Assert.That(Row("bark").ElasticModulus, Is.EqualTo(1.28e10), "green black locust, the Wood Handbook");
            Assert.That(Row("deadwood").ElasticModulus, Is.EqualTo(7.0e9));
            MaterialRow straw = Row("straw");
            Assert.That(straw.HasWet, Is.True);
            Assert.That(straw.WetStiffness, Is.EqualTo(straw.Stiffness * straw.WetStiffnessRatio));
            Assert.That(straw.WetStaticFriction >= straw.WetKineticFriction && straw.WetAdhesion > 0 && straw.WetAdhesionRelease > 0, Is.True);
            Assert.That(Array.FindAll(rows, r => r.HasWet).Length, Is.EqualTo(1), "only straw carries a wet block");

            string noLaw = Edit(MaterialsText(), "\"straw\": {", "\"area_law\":", "\"area_lawx\":");
            string m = Assert.Throws<FormatException>(() => PhysicsMaterials.Parse(noLaw, out _)).Message;
            TestContext.Out.WriteLine(m);
            StringAssert.Contains("materials.straw.area_law: missing", m);
            string noModulus = Edit(MaterialsText(), "\"bark\": {", "\"elastic_modulus_pa\":", "\"elastic_modulusx\":");
            StringAssert.Contains("materials.bark.elastic_modulus_pa: missing", Assert.Throws<FormatException>(() => PhysicsMaterials.Parse(noModulus, out _)).Message);
            string badWet = Edit(MaterialsText(), "\"straw\": {|\"wet\"|\"stiffness_n_per_m\"", "294.0", "300.0");
            StringAssert.Contains("materials.straw.wet.stiffness_n_per_m: is labelled derived", Assert.Throws<FormatException>(() => PhysicsMaterials.Parse(badWet, out _)).Message);
            string unlabelled = Edit(MaterialsText(), "\"fabric\": {|\"area_reference_depth_m\"", "\"label\": \"assumed\", ", "");
            StringAssert.Contains("materials.fabric.area_reference_depth_m.label: missing", Assert.Throws<FormatException>(() => PhysicsMaterials.Parse(unlabelled, out _)).Message);
        }
    }
}
