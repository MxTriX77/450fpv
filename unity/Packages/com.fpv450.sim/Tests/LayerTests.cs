using System;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// ground-contact's compliant layers (p7a-ground 1.12, design 6): a layer row's stiffness is the 15 mm reference foot's
    /// and a touching part meets it by its own footprint, by the row's area law (straw a mat spreading 2:1 through its depth,
    /// K§7.1 step 4; fabric a flat punch, Sneddon 1965; a sandbag's fill Winkler's); a layer compresses at most 80 % of its
    /// thickness (K§7.1's mat constant, assumed), past which it is solid and carries the part onto its own object's soil
    /// beneath, or leaves the part's own spring. Every target runs a known-wrong model that must fail it.
    /// </summary>
    public class LayerTests
    {
        const double RadToDeg = 57.29577951308232;

        static DroneSpec _drone;
        static DroneSpec Drone() => _drone ??= DroneSpec.Parse(File.ReadAllText(ContactScenarios.RepositoryFile("assets-src/drone/drone.json")));

        static MaterialRow Row(string name) => Array.Find(ContactScenarios.RepositoryMaterials(out _), r => r.Name == name);

        // ---- The area laws ------------------------------------------------------------------------------------------

        [Test]
        public void TheAreaLawsScaleAsTheTableSays()
        {
            // physics_materials.json's notes (derived): a half-skid (0.22 × 0.01 m) on S04's straw meets 3.03 × the reference
            // foot's 490 N/m, on fabric 3.5 ×, on a sandbag 12.4 ×; the 15 mm foot itself 1 × by every law.
            MaterialRow straw = Row("straw"), fabric = Row("fabric"), sandbag = Row("sandbag");
            double mat = Layer.Scale(straw.AreaLaw, straw.AreaReferenceDepth, SoilScenarios.MatDepth, false, 0.22, 0.01);
            double punch = Layer.Scale(fabric.AreaLaw, fabric.AreaReferenceDepth, LayerScenarios.FabricDepth, false, 0.22, 0.01);
            double winkler = Layer.Scale(sandbag.AreaLaw, sandbag.AreaReferenceDepth, sandbag.AreaReferenceDepth, false, 0.22, 0.01);
            TestContext.WriteLine($"  a half-skid meets straw {mat:F4} ×, fabric {punch:F4} ×, a sandbag {winkler:F4} × the reference foot's");
            Assert.That(straw.AreaLaw, Is.EqualTo(AreaLaw.Mat));
            Assert.That(fabric.AreaLaw, Is.EqualTo(AreaLaw.Punch));
            Assert.That(sandbag.AreaLaw, Is.EqualTo(AreaLaw.Winkler));
            Assert.That(mat, Is.EqualTo(3.03).Within(1).Percent, "straw, the 2:1 spread at the mat's depth");
            Assert.That(punch, Is.EqualTo(3.5).Within(1).Percent, "fabric, the punch's equivalent diameter");
            Assert.That(winkler, Is.EqualTo(12.4).Within(1).Percent, "a sandbag's fill, the footprint's area");
            foreach (MaterialRow r in new[] { straw, fabric, sandbag })
                Assert.That(Layer.Scale(r.AreaLaw, r.AreaReferenceDepth, r.AreaReferenceDepth, true, 0, Layer.ReferenceFoot), Is.EqualTo(1).Within(1e-12),
                    r.Name + ": the reference foot meets the row's own stiffness");

            // A capsule's footprint: its length on the face, at least its end cap's; standing on its end, its cap's.
            Double3 up = new Double3(0, 1, 0);
            Assert.That(Layer.Pressed(new Double3(0, 0.004, 0), new Double3(0, 0.004, 0.22), 0.005, up), Is.EqualTo(0.22).Within(1e-15), "level: its whole length");
            Assert.That(Layer.Pressed(new Double3(0, 0, 0), new Double3(0, 0.11, 0.22), 0.005, up), Is.EqualTo(0.22).Within(1e-15), "tilted: its projection");
            Assert.That(Layer.Pressed(new Double3(0, 0.004, 0), new Double3(0, 0.104, 0), 0.005, up), Is.EqualTo(0.5 * Math.PI * 0.005).Within(1e-15),
                "on its end: its cap's");
        }

        [Test]
        public void ASkidMeetsTheStrawByItsArea()
        {
            // ground-contact's "A skid meets the straw by its area": the drone and the reference-foot drone (K§7.1's leg, one
            // 15 mm foot at each T-joint) each pressed level into S04's 0.125 m straw on the test range to their weight, by the
            // contact model alone; each corner's layer stiffness is its load over the layer's compression (the overlap less the
            // leg's deflection). The half-skid meets 3.03 × the foot's 490 N/m within 1 %; the known-wrong (the straw row
            // without its area law) meets the foot's, so the drone presses about twice as deep (2.76 × by the law and the leg,
            // less with the leg tubes' ends meeting the straw past 15 mm), and fails.
            DroneSpec spec = Drone();
            MaterialRow straw = Row("straw");
            FlightWorld range = new FlightWorld(RangeScenarios.World);
            Double3 top = LayerScenarios.Tile;
            LayerScenarios.Press skid = LayerScenarios.Pressing(LayerScenarios.Setup(spec, range), top, 1);
            LayerScenarios.Press foot = LayerScenarios.Pressing(LayerScenarios.ReferenceFeet(LayerScenarios.Setup(spec, range)), top, 1);
            LayerScenarios.Press wrong = LayerScenarios.Pressing(LayerScenarios.Setup(spec, range, without: "straw"), top, 1);
            double half = LayerScenarios.HalfSkid(straw, SoilScenarios.MatDepth);
            for (int c = 0; c < 4; c++)
            {
                double ks = skid.Load[c] / skid.Compression[c], kf = foot.Load[c] / foot.Compression[c], kw = wrong.Load[c] / wrong.Compression[c];
                TestContext.WriteLine($"  corner {c}: the half-skid {ks:F3} N/m ({ks / kf:F4} × the foot's {kf:F3}; the law's {half:F3}), "
                    + $"pressed {skid.Overlap[c] * 1e3:F3} mm at {skid.Load[c]:F3} N; without the law {kw:F3} N/m, {wrong.Overlap[c] * 1e3:F3} mm "
                    + $"({wrong.Overlap[c] / skid.Overlap[c]:F3} × deeper)");
                Assert.That(kf, Is.EqualTo(straw.Stiffness).Within(1e-9).Percent, "the reference foot meets the row's own 490 N/m");
                Assert.That(ks, Is.EqualTo(half).Within(1e-9).Percent, "the half-skid meets the law's");
                Assert.That(ks / kf, Is.EqualTo(3.03).Within(1).Percent, "3.03 × the foot's");
                Assert.That(kw / kf, Is.Not.EqualTo(3.03).Within(1).Percent, "known-wrong: without the area law it is the foot's");
                Assert.That(wrong.Overlap[c] / skid.Overlap[c], Is.GreaterThan(1.5), "so it presses about twice as deep");
            }
            Assert.That(skid.Allocated + foot.Allocated, Is.EqualTo(0), "0 B a step on a layer");
        }

        // ---- The stroke ---------------------------------------------------------------------------------------------

        [Test]
        public void AHardLandingOnAMattressStopsAtItsStroke()
        {
            // ground-contact's "A hard landing on a mattress stops at its stroke": the drone pressed level past each layer's
            // stroke load, quasi-static (the stroke's static law; a 1 m drop onto a mattress peaks near 10 g). On a 0.15 m
            // fabric box over the stub's rigid floor to 10 g: each corner's fabric compresses to its stroke, 0.12 m, and no
            // further; past it the leg alone takes the load. On S04's 0.125 m straw over its own soil box (the test range, with
            // surfaces.json) to 20 g: the straw stops at its 0.1 m and the further sink goes into the soil by the soil's law,
            // the mat's solid 25 mm carrying the skid onto it. The known-wrong, no stroke limit, compresses each layer past
            // its own thickness, which no layer can.
            DroneSpec spec = Drone();
            MaterialRow fabric = Row("fabric"), straw = Row("straw");
            double k0;

            Double3 mattress = new Double3(0, LayerScenarios.FabricDepth, 0);
            LayerScenarios.Press soft = LayerScenarios.Pressing(LayerScenarios.Setup(spec, LayerScenarios.MattressFloor()), mattress, 10,
                place: LayerScenarios.PlaceMattress);
            LayerScenarios.Press softWrong = LayerScenarios.Pressing(LayerScenarios.Setup(spec, LayerScenarios.MattressFloor()), mattress, 10,
                double.PositiveInfinity, LayerScenarios.PlaceMattress);
            k0 = soft.LegStiffness;
            double fabricStroke = Layer.StrokeShare * LayerScenarios.FabricDepth, kf = LayerScenarios.HalfSkid(fabric, LayerScenarios.FabricDepth);
            for (int c = 0; c < 4; c++)
            {
                TestContext.WriteLine($"  the mattress, corner {c}: {soft.Load[c]:F2} N ({soft.Load[c] / (soft.Weight / 4):F2} g on its share; the stroke at "
                    + $"{kf * fabricStroke:F2} N), the fabric {soft.Compression[c] * 1e3:F3} mm, the leg {soft.Deflection[c] * 1e3:F3} mm; without the "
                    + $"stroke the fabric {softWrong.Compression[c] * 1e3:F3} mm at {softWrong.Load[c]:F2} N, the skid {(softWrong.Overlap[c] - LayerScenarios.FabricDepth) * 1e3:F1} mm into the floor");
                Assert.That(soft.Load[c], Is.GreaterThan(kf * fabricStroke), "pressed past the stroke's load");
                Assert.That(soft.Compression[c], Is.EqualTo(fabricStroke).Within(1e-9), "the fabric stops at 80 % of its 0.15 m");
                Assert.That(soft.Load[c], Is.EqualTo(k0 * (soft.Overlap[c] - fabricStroke)).Within(1e-9).Percent, "past it the leg alone takes the load");
                Assert.That(softWrong.Compression[c], Is.GreaterThan(LayerScenarios.FabricDepth), "known-wrong: past the mattress's own thickness");
            }
            Assert.That(soft.Total, Is.GreaterThanOrEqualTo(10 * soft.Weight), "the press reached 10 g");
            Assert.That(soft.Allocated, Is.EqualTo(0), "0 B a step at the stroke");

            FlightWorld range = new FlightWorld(RangeScenarios.World);
            SurfaceTable table = SoilScenarios.Table();
            LayerScenarios.Press mat = LayerScenarios.Pressing(LayerScenarios.Setup(spec, range, table), LayerScenarios.Tile, 20);
            LayerScenarios.Press matWrong = LayerScenarios.Pressing(LayerScenarios.Setup(spec, range, table), LayerScenarios.Tile, 20, double.PositiveInfinity);
            double strawStroke = Layer.StrokeShare * SoilScenarios.MatDepth, ks = LayerScenarios.HalfSkid(straw, SoilScenarios.MatDepth);
            SoilSet soil = Array.Find(table.Surfaces, x => x.Name == "belt_straw").Dry;
            double modulus = Soil.Modulus(soil, Soil.Width(SoilPart.Tube, 0.005));
            for (int c = 0; c < 4; c++)
            {
                ContactModel.SkidSoil sk = mat.Soil[c];
                double z = mat.Overlap[c] - mat.Deflection[c] - strawStroke, layer = mat.Compression[c] - sk.Sink1;
                double law = Soil.Force(SoilPart.Tube, 0.005, soil, modulus, table.FirmLayer, z, z, out _) * 0.22;
                TestContext.WriteLine($"  S04's straw, corner {c}: {mat.Load[c]:F2} N ({mat.Load[c] / (mat.Weight / 4):F2} g on its share; the stroke at "
                    + $"{ks * strawStroke:F2} N), the straw {layer * 1e3:F3} mm, the soil under it {sk.Sink1 * 1e3:F3} mm (overlap less the leg and "
                    + $"the stroke {z * 1e3:F3}) carrying {sk.Static:F3} N (its law {law:F3}), the straw {mat.Load[c] - sk.Static:F3} N, residual {sk.Residual:E1} N; "
                    + $"without the stroke the straw {matWrong.Compression[c] * 1e3:F3} mm");
                Assert.That(sk.Engaged, Is.True, "the soil box carries the skid");
                Assert.That(RangeScenarios.World.SurfaceName(sk.Surface), Is.EqualTo("belt_straw"));
                Assert.That(RangeScenarios.World.ObjectName(sk.Object), Does.StartWith("S04"), "the straw's own object's soil");
                Assert.That(mat.Load[c], Is.GreaterThan(ks * strawStroke), "pressed past the stroke's load");
                Assert.That(layer, Is.EqualTo(strawStroke).Within(1e-9), "the straw stops at 80 % of its 0.125 m");
                Assert.That(mat.Load[c] - sk.Static, Is.EqualTo(ks * strawStroke).Within(1e-6).Percent, "the straw at its stroke carries its stroke's load");
                Assert.That(sk.Sink1, Is.EqualTo(z).Within(1e-12), "the soil sinks by what the skid moved past the stroke");
                Assert.That(sk.Static, Is.EqualTo(law).Within(1e-9).Percent, "by the soil's own law");
                Assert.That(Math.Abs(sk.Residual), Is.LessThan(1e-9), "the leg in series with both");
                Assert.That(matWrong.Compression[c], Is.GreaterThan(SoilScenarios.MatDepth), "known-wrong: past the mat's own thickness");
            }
            Assert.That(mat.Total, Is.GreaterThanOrEqualTo(20 * mat.Weight), "the press reached 20 g");
            Assert.That(mat.Allocated, Is.EqualTo(0), "0 B a step carried onto the soil");
        }

        // ---- The straw rest in rain ----------------------------------------------------------------------------------

        [Test]
        public void TheRangeFlightInRainRestsLevelOnWetStraw()
        {
            // 1.13's finding: the range golden's flight in Rain with surfaces.json ended rolled 17.6° on wet straw (1.3° dry),
            // its half-skids on the reference foot's 294 N/m. By its area the wet mat meets each half-skid at 3.03 × that, and
            // the flight rests level; the known-wrong (the straw row without its area law) rolls past 10° again.
            DroneSpec spec = Drone();
            foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain })
            {
                GoldenFlights.FlyRange(spec, out FlightCore core, rain: rain, table: SoilScenarios.Table());
                double roll = Roll(core.State);
                TestContext.WriteLine($"  {rain}: rests rolled {roll:F3}°, pitched {Pitch(core.State):F3}°, {core.Contacts.ImpactCount} impacts, crashed {core.State.Crashed}");
                Assert.That(Math.Abs(roll), Is.LessThan(2), rain + ": level on the straw");
                Assert.That(core.State.Crashed, Is.False);
            }
            MaterialRow[] without = (MaterialRow[])ContactScenarios.RepositoryMaterials(out _).Clone();
            for (int i = 0; i < without.Length; i++)
                if (without[i].Name == "straw") without[i].AreaLaw = AreaLaw.None;
            GoldenFlights.FlyRange(spec, out FlightCore wrong, rain: RainLevel.Rain, table: SoilScenarios.Table(), materials: without);
            TestContext.WriteLine($"  known-wrong (straw without its area law), Rain: rests rolled {Roll(wrong.State):F3}°");
            Assert.That(Math.Abs(Roll(wrong.State)), Is.GreaterThan(10), "known-wrong: the reference foot's stiffness rolls it over on wet straw");
        }

        // ---- Determinism, replay and allocations ---------------------------------------------------------------------

        [Test]
        public void ADropOntoWetStrawReplaysBitForBit()
        {
            // The drone dropped disarmed from 1.5 m onto S04's straw tile in Rain with surfaces.json: it lands at 5.4 m/s, the
            // wet mat meets each half-skid by its area, passes its stroke and carries the skids onto its soil box, then holds
            // the drone at rest; recorded with its header and replayed headless on the same world, every step bit for bit,
            // with nothing allocated while the layer and the soil act.
            DroneSpec spec = Drone();
            var world = new FlightWorld(SoilScenarios.Range());
            FlightSetup setup = SoilScenarios.Setup(spec, world, LayerScenarios.Tile + new Double3(0, SoilScenarios.Standing(spec) + 1.5, 0), RainLevel.Rain, 7, true);
            var core = new FlightCore(setup);
            Log.LogHeader header = Log.LogHeader.For(setup, core);
            header.WorldId = "test-range";
            header.SetDrone(spec, spec.Nominal);
            var stream = new MemoryStream();
            var recorder = new Log.FlightRecorder(stream, header, Log.FlightRecorder.DefaultCapacity, ownsStream: false);
            bool carried = false;
            long allocated = 0;
            for (int k = 0; k < 2500; k++)
            {
                var stick = new Control.StickSample { HostUs = k * FlightCore.StepUs };
                long before = GC.GetAllocatedBytesForCurrentThread();
                core.Advance(stick);
                if (k >= 500) allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
                for (int c = 0; c < 4; c++) carried |= core.Contacts.SoilOf(c).Engaged && core.Contacts.SoilOf(c).Static > 0;
            }
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            Assert.That(core.State.Crashed, Is.False);
            Assert.That(carried, Is.True, "the mat passed its stroke and carried a skid onto its soil");
            Assert.That(allocated, Is.EqualTo(0), "0 B a step on the layer and its soil");
            ContactScenarios.RepositoryMaterials(out ulong materials); // the replay holds the flight's physics_materials.json (p7f-damage 1.1a)
            Log.ReplayResult r = Log.FlightReplay.Run(new MemoryStream(stream.ToArray()), id => id == "test-range" ? world : null, spec.ContentHash, null,
                SoilScenarios.Table().Hash, materialsHash: materials);
            TestContext.WriteLine($"  replay: {r.Outcome}, {r.Steps} steps; {r.Message}");
            Assert.That(r.Outcome, Is.EqualTo(Log.ReplayOutcome.Match), r.Message);
            Assert.That(r.Steps, Is.EqualTo(2500));
        }

        static double Roll(in FlightState s) => Math.Asin(s.Attitude.Rotate(new Double3(1, 0, 0)).Y) * RadToDeg;

        static double Pitch(in FlightState s) => Math.Asin(s.Attitude.Rotate(new Double3(0, 0, 1)).Y) * RadToDeg;
    }
}
