using System;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// ground-contact's wet ground (p7a-ground 1.13, design 7; D-021: "Sunny means dry"): in Rain every surface takes its
    /// wet set and in Severe rain its saturated set; a groove that has loaded wet soil holds its half-skid as it lifts, by
    /// a cohesive zone's law (Dugdale 1960, Barenblatt 1962) over its wetted footprint with the normal adhesion σ_a of the
    /// order of kPa (Fountaine 1954; the breakout suction under a lifted object, Vesic 1971, Foda 1982); wet straw is
    /// softer and clings; the drawn elements read the cover's wet sets. Every target runs a known-wrong model that must
    /// fail it.
    /// </summary>
    public class WetGroundTests
    {
        const double R = 0.005, L = 0.22; // drone.json's skid tube (measured)

        static DroneSpec _drone;
        static DroneSpec Drone() => _drone ??= DroneSpec.Parse(File.ReadAllText(ContactScenarios.RepositoryFile("assets-src/drone/drone.json")));

        static SoilSet Set(string surface, Moisture m) => Array.Find(SoilScenarios.Table().Surfaces, s => s.Name == surface).Set(m);

        static MaterialRow Straw()
        {
            foreach (MaterialRow r in ContactScenarios.RepositoryMaterials(out _))
                if (r.Name == "straw") return r;
            throw new InvalidOperationException("physics_materials.json has no straw row");
        }

        static double Series(double a, double b) => a * b / (a + b);

        // ---- A wet skid is held as it lifts ----------------------------------------------------------------------------

        // The scenario's checks on one lift: each half-skid's pull rises to σ_a times its wetted footprint within 1 %, falls
        // to nothing by δ_a, and does ½·σ_a·δ_a times the footprint of work within 1 %; nothing allocated on the way.
        static void AssertHeld(SoilScenarios.Unstick u, double sigma, double release, double footprint, string name)
        {
            Assert.That(u.Load, Is.GreaterThanOrEqualTo(u.Weight), name + ": pressed to the resting load");
            for (int c = 0; c < 4; c++)
            {
                double hold = sigma * footprint, work = 0.5 * sigma * release * footprint;
                TestContext.WriteLine($"  {name} corner {c}: the footprint {u.Footprint[c] * 1e6:F1} mm² (2r·L {footprint * 1e6:F1}); the pull peaks at {u.Peak[c]:F4} N "
                    + $"(σ_a·S {hold:F4}, {(u.Peak[c] / hold - 1) * 100:F3} %), last acts {u.Released[c] * 1e3:F4} mm above the set (δ_a {release * 1e3:F1} mm); "
                    + $"its work {u.Work[c] * 1e3:F5} mJ (½·σ_a·δ_a·S {work * 1e3:F5}, {(u.Work[c] / work - 1) * 100:F3} %)");
                Assert.That(u.Footprint[c], Is.EqualTo(footprint).Within(1e-9).Percent, name + ": sunk past the tube's radius, the footprint is its full width");
                Assert.That(u.Peak[c], Is.EqualTo(hold).Within(1).Percent, name + ": the pull rises to σ_a·S_m");
                Assert.That(u.Peak[c], Is.LessThanOrEqualTo(hold * (1 + 1e-9)), name + ": and no further");
                Assert.That(u.Released[c], Is.LessThan(release), name + ": nothing past δ_a");
                Assert.That(u.Released[c], Is.GreaterThan(release - 5e-5), name + ": the pull falls to zero by δ_a, not before");
                Assert.That(u.Work[c], Is.EqualTo(work).Within(1).Percent, name + ": the work to part is ½·σ_a·δ_a·S_m");
                Assert.That(u.Holding[c], Is.False, name + ": let go");
            }
            Assert.That(u.Allocated, Is.EqualTo(0), name + ": 0 B a step while the groove holds and lets go");
        }

        [Test]
        public void AWetSkidIsHeldAsItLifts()
        {
            DroneSpec spec = Drone();
            foreach (RainLevel rain in new[] { RainLevel.Rain, RainLevel.SevereRain })
            {
                SoilSet set = Set("belt_straw", SurfaceTable.MoistureOf(rain));
                SoilScenarios.Unstick u = SoilScenarios.Unsticking(spec, SoilScenarios.Floor("belt_straw"), default, rain);
                AssertHeld(u, set.Adhesion, set.AdhesionRelease, 2 * R * L, "belt_straw " + rain);
            }

            // In the dry soil no pull acts.
            SoilScenarios.Unstick dry = SoilScenarios.Unsticking(spec, SoilScenarios.Floor("belt_straw"), default, RainLevel.Sunny);
            for (int c = 0; c < 4; c++) Assert.That(dry.Peak[c], Is.EqualTo(0), "dry belt_straw holds nothing as it lifts");

            // Known-wrong: 1.10's wet soil, the interface adhesion c_a in the anchor and no cohesive pull. It acts along the
            // ground and lets go with the normal force, so nothing holds the lift, and it fails.
            SurfaceTable without = SoilScenarios.With("belt_straw", x => { x.Adhesion = 0; return x; });
            SoilScenarios.Unstick wrong = SoilScenarios.Unsticking(spec, SoilScenarios.Floor("belt_straw"), default, RainLevel.Rain, without);
            double target = Set("belt_straw", Moisture.Wet).Adhesion * 2 * R * L;
            TestContext.WriteLine($"  known-wrong (no cohesive pull) in Rain: the pull peaks at {wrong.Peak[0]:F4} N against {target:F4}");
            for (int c = 0; c < 4; c++)
                Assert.That(wrong.Peak[c], Is.Not.EqualTo(target).Within(1).Percent, "the wet soil without its cohesive pull fails the hold");
        }

        [Test]
        public void TheCohesiveLawIsContinuousAndPeaksAtSigmaA()
        {
            // Soil.Cohesion alone: it starts at 0 at the set (Wong's unloading ends there at 0), rises on the unload line to
            // σ_a·w at σ_a/k_u, falls to 0 at δ_a, and its area is ½·σ_a·δ_a·w (Simpson's rule on each straight piece, exact).
            foreach (Moisture m in new[] { Moisture.Wet, Moisture.Saturated })
            {
                SoilSet set = Set("belt_straw", m);
                double ku = set.UnloadRatio * Soil.Modulus(set, 2 * R), w = 2 * R, se = set.Adhesion / ku;
                Assert.That(Soil.Cohesion(set, ku, w, 0, out _), Is.EqualTo(0));
                Assert.That(Soil.Cohesion(set, ku, w, se, out _), Is.EqualTo(set.Adhesion * w).Within(1e-12).Percent);
                Assert.That(Soil.Cohesion(set, ku, w, set.AdhesionRelease, out _), Is.EqualTo(0));
                double area = 0;
                for (int i = 0; i < 2; i++)
                {
                    double a = i == 0 ? 0 : se, b = i == 0 ? se : set.AdhesionRelease, h = b - a;
                    area += h / 6 * (Soil.Cohesion(set, ku, w, a + 1e-15, out _) + 4 * Soil.Cohesion(set, ku, w, 0.5 * (a + b), out _)
                        + Soil.Cohesion(set, ku, w, b - 1e-15, out _));
                }
                TestContext.WriteLine($"  belt_straw {m}: k_u {ku:E3} N/m³, the bond holds to {se * 1e3:F3} mm then fails by {set.AdhesionRelease * 1e3:F1} mm; "
                    + $"the work per metre {area:E6} J/m against ½·σ_a·δ_a·w {0.5 * set.Adhesion * set.AdhesionRelease * w:E6}");
                Assert.That(area, Is.EqualTo(0.5 * set.Adhesion * set.AdhesionRelease * w).Within(1e-9).Percent);
            }
            // The dry set has no pull at any separation.
            SoilSet dry = Set("belt_straw", Moisture.Dry);
            Assert.That(Soil.Cohesion(dry, dry.UnloadRatio * Soil.Modulus(dry, 2 * R), 2 * R, 0.001, out _), Is.EqualTo(0));
        }

        // ---- Rain softens the ground -----------------------------------------------------------------------------------

        // The deepest node any half-skid rests at, m.
        static double Sink(SoilScenarios.Rest r)
        {
            double sink = 0;
            for (int c = 0; c < 4; c++) sink = Math.Max(sink, Math.Max(r.Skids[c].Sink0, Math.Max(r.Skids[c].Sink1, r.Skids[c].Sink2)));
            return sink;
        }

        // "Deeper": by more than 1 %, far past what the rain's water film's own weight sinks the dry rows (0.0009 % on tilled, the
        // known-wrong prints it).
        const double Deeper = 0.01;

        [Test]
        public void RainSoftensTheGround()
        {
            DroneSpec spec = Drone();
            SurfaceTable table = SoilScenarios.Table();
            var range = new FlightWorld(SoilScenarios.Range());
            var rains = new[] { RainLevel.Sunny, RainLevel.Rain, RainLevel.SevereRain };

            // The range's soils: its zones' on the stub's floor until 1.3 lands them, and the plane's belt_straw at the track
            // start. Each wetter set sinks the skids deeper than the drier.
            var places = new (string Name, FlightWorld World, Double3 At)[]
            {
                ("dirt_track (the track's zone)", SoilScenarios.Floor("dirt_track"), default), ("tilled (the field's zone)", SoilScenarios.Floor("tilled"), default),
                ("crater_spoil (the shelled patch's zone)", SoilScenarios.Floor("crater_spoil"), default),
                ("the plane's belt_straw at the track start", range, SoilScenarios.TrackStart(range)),
            };
            foreach (var p in places)
            {
                double last = 0;
                string line = "";
                foreach (RainLevel rain in rains)
                {
                    double sink = Sink(SoilScenarios.Resting(spec, p.World, p.At, rain));
                    line += $"{rain} {sink * 1e3:F3} mm  ";
                    Assert.That(sink, Is.GreaterThan(last * (1 + Deeper)), p.Name + ": " + rain + " sinks deeper than the drier set");
                    last = sink;
                }
                TestContext.WriteLine($"  {p.Name}: {line}");
            }

            // Straw (the drone never reaches S04's soil box): wet straw is softer. On one level mat each corner meets the
            // row's wet stiffness in series with the leg (its load over its overlap), and the weight presses it deeper
            // (the leg tubes, body parts, take a share of it on the mat); Rain and Severe rain share the row's one wet
            // block. On S04's own tiled mat at its patch's centre every corner presses deeper too.
            MaterialRow straw = Straw();
            double k0 = Drone().BuildContacts(Drone().Nominal, ContactScenarios.RepositoryMaterials(out ulong mh), mh).LegStiffness;
            double ratio = Series(k0, straw.Stiffness) / Series(k0, straw.WetStiffness);
            Double3 top = new Double3(0, SoilScenarios.MatDepth, 0), patch = SoilScenarios.PatchCentre(range);
            SoilScenarios.Unstick dryMat = SoilScenarios.Unsticking(spec, SoilScenarios.MatFloor(), top, RainLevel.Sunny, place: SoilScenarios.PlaceMat);
            SoilScenarios.Unstick dryPatch = SoilScenarios.Unsticking(spec, range, patch, RainLevel.Sunny);
            foreach (RainLevel rain in new[] { RainLevel.Rain, RainLevel.SevereRain })
            {
                SoilScenarios.Unstick wetMat = SoilScenarios.Unsticking(spec, SoilScenarios.MatFloor(), top, rain, place: SoilScenarios.PlaceMat);
                SoilScenarios.Unstick wetPatch = SoilScenarios.Unsticking(spec, range, patch, rain);
                for (int c = 0; c < 4; c++)
                {
                    double kw = wetMat.PressLoad[c] / wetMat.Press[c], kd = dryMat.PressLoad[c] / dryMat.Press[c];
                    TestContext.WriteLine($"  straw corner {c}: on a level mat {rain} {wetMat.Press[c] * 1e3:F2} mm at {wetMat.PressLoad[c]:F3} N, {kw:F3} N/m "
                        + $"(the wet row with the leg {Series(k0, straw.WetStiffness):F3}), against Sunny {dryMat.Press[c] * 1e3:F2} mm at {dryMat.PressLoad[c]:F3} N, {kd:F3} N/m "
                        + $"({Series(k0, straw.Stiffness):F3}; {ratio:F4}× stiffer); on S04's patch {wetPatch.Press[c] * 1e3:F2} against {dryPatch.Press[c] * 1e3:F2} mm");
                    Assert.That(kw, Is.EqualTo(Series(k0, straw.WetStiffness)).Within(1e-9).Percent, "wet straw meets the row's wet stiffness");
                    Assert.That(kd, Is.EqualTo(Series(k0, straw.Stiffness)).Within(1e-9).Percent, "dry straw the row's own");
                    Assert.That(wetMat.Press[c], Is.GreaterThan(dryMat.Press[c]), "the wet mat sinks deeper at the weight");
                    Assert.That(wetPatch.Press[c], Is.GreaterThan(dryPatch.Press[c]), "S04's wet mat sinks deeper");
                }
            }

            // The log header names the rain level and the rows, and the set each surface reads is the level's.
            foreach (RainLevel rain in rains)
            {
                FlightSetup setup = SoilScenarios.Setup(spec, SoilScenarios.Floor("belt_straw"), new Double3(0, SoilScenarios.Standing(spec), 0), rain);
                var core = new FlightCore(setup);
                LogHeader h = LogHeader.Read(new MemoryStream(LogHeader.For(setup, core).Encode()));
                Assert.That(h.Rain, Is.EqualTo(rain));
                SurfaceTable logged = h.Contacts.Surfaces;
                Assert.That(logged.Hash, Is.EqualTo(table.Hash));
                for (int i = 0; i < table.Surfaces.Length; i++)
                {
                    Assert.That(logged.Surfaces[i].Name, Is.EqualTo(table.Surfaces[i].Name));
                    Assert.That(logged.Surfaces[i].Set(SurfaceTable.MoistureOf(h.Rain)), Is.EqualTo(table.Surfaces[i].Set(SurfaceTable.MoistureOf(rain))), table.Surfaces[i].Name);
                }
                MaterialRow loggedStraw = Array.Find(h.Contacts.Materials, r => r.Name == "straw");
                Assert.That(loggedStraw, Is.EqualTo(straw), "the straw row with its wet block");
            }

            // Known-wrong: the dry rows in rain (each surface's wet and saturated sets its dry one) sink no deeper, so it fails.
            SurfaceTable dryRows = table.Clone();
            for (int i = 0; i < dryRows.Surfaces.Length; i++) dryRows.Surfaces[i].Wet = dryRows.Surfaces[i].Saturated = dryRows.Surfaces[i].Dry;
            double sunny = Sink(SoilScenarios.Resting(spec, SoilScenarios.Floor("tilled"), default, RainLevel.Sunny, dryRows));
            double rainy = Sink(SoilScenarios.Resting(spec, SoilScenarios.Floor("tilled"), default, RainLevel.Rain, dryRows));
            TestContext.WriteLine($"  known-wrong (the dry rows in rain), tilled: Sunny {sunny * 1e3:F6} mm, Rain {rainy * 1e3:F6} mm, "
                + $"{(rainy / sunny - 1) * 100:F4} % deeper (the rain's water film on the airframe)");
            Assert.That(rainy > sunny * (1 + Deeper), Is.False, "the dry rows in rain fail");
        }

        // ---- Wet straw clings --------------------------------------------------------------------------------------

        [Test]
        public void WetStrawClingsAsItLifts()
        {
            // Lifted at 10 mm/s off a level straw mat (S04's row and depth, one box) after pressing it to the weight: the
            // straw row's wet block holds each corner by its σ_a over the half-skid's footprint (sunk 40 mm, past the tube's
            // radius: 2r·L), falling to nothing by its δ_a; dry straw holds nothing (the known-wrong: the mat before 1.13).
            DroneSpec spec = Drone();
            Double3 top = new Double3(0, SoilScenarios.MatDepth, 0);
            MaterialRow straw = Straw();
            foreach (RainLevel rain in new[] { RainLevel.Rain, RainLevel.SevereRain })
                AssertHeld(SoilScenarios.Unsticking(spec, SoilScenarios.MatFloor(), top, rain, place: SoilScenarios.PlaceMat), straw.WetAdhesion,
                    straw.WetAdhesionRelease, 2 * R * L, "the straw mat " + rain);
            SoilScenarios.Unstick dry = SoilScenarios.Unsticking(spec, SoilScenarios.MatFloor(), top, RainLevel.Sunny, place: SoilScenarios.PlaceMat);
            for (int c = 0; c < 4; c++) Assert.That(dry.Peak[c], Is.EqualTo(0), "dry straw holds nothing");
        }

        // ---- Wet cover --------------------------------------------------------------------------------------------

        [Test]
        public void RainReadsTheWetCover()
        {
            // The drawn elements' mechanics at the flight's moisture (1.14 and 1.15 read them): the cover's wet set in both
            // rains, so a wet straw or stem holds a hook harder before it breaks and a wet straw costs more to cut.
            DroneSpec spec = Drone();
            SurfaceTable table = SoilScenarios.Table();
            FlightWorld world = SoilScenarios.Floor("belt_straw");
            foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain, RainLevel.SevereRain })
            {
                FlightSetup setup = SoilScenarios.Setup(spec, world, default, rain);
                var model = new ContactModel(setup.Contacts, world, setup.Mass.Properties.Mass, SurfaceTable.MoistureOf(rain));
                for (int k = 0; k < table.Cover.Length; k++)
                {
                    CoverSet want = rain == RainLevel.Sunny ? table.Cover[k].Dry : table.Cover[k].Wet;
                    Assert.That(model.Cover((CoverKind)k), Is.EqualTo(want), rain + " " + (CoverKind)k);
                }
            }
            CoverSet dry = table.Cover[(int)CoverKind.Straw].Dry, wet = table.Cover[(int)CoverKind.Straw].Wet;
            TestContext.WriteLine($"  straw: hook release {dry.HookReleaseMin:F3}–{dry.HookReleaseMax:F3} N dry, {wet.HookReleaseMin:F3}–{wet.HookReleaseMax:F3} wet; "
                + $"cut energy {dry.CutEnergy:F0} J/m² dry, {wet.CutEnergy:F0} wet; E {dry.ModulusMin:E2}–{dry.ModulusMax:E2} Pa dry, {wet.ModulusMin:E2}–{wet.ModulusMax:E2} wet");
            Assert.That(wet.HookReleaseMin, Is.GreaterThan(dry.HookReleaseMin), "tougher to break");
            Assert.That(wet.CutEnergy, Is.GreaterThan(dry.CutEnergy), "tougher to cut");
            CoverSet stemDry = table.Cover[(int)CoverKind.Stem].Dry, stemWet = table.Cover[(int)CoverKind.Stem].Wet;
            Assert.That(stemWet.HookReleaseMin, Is.GreaterThan(stemDry.HookReleaseMin), "a wet stem holds a hook harder");

            // Without the table nothing is read.
            FlightSetup rigid = SoilScenarios.Setup(spec, world, default, RainLevel.Rain, rigid: true);
            var none = new ContactModel(rigid.Contacts, world, rigid.Mass.Properties.Mass, Moisture.Wet);
            Assert.That(none.Cover(CoverKind.Straw), Is.EqualTo(default(CoverSet)));
        }

        // ---- The audit: the pull is the whole of the move -------------------------------------------------------------

        /// <summary>The contact golden's flight (GoldenFlights.FlyContact) in Rain and in Severe rain with surfaces.json:
        /// model 27's hashes (measured on its build, 01848ba4), and model 32's.</summary>
        public const ulong Model27ContactRainHash = 0x5EEAD2BA365E08A3UL, Model27ContactSevereHash = 0x8D9D8D823D0AEA3EUL;
        public const ulong ContactRainHash = 0xACEC8D0EA9C542FDUL, ContactSevereHash = 0x9E716F454B792884UL;

        [Test]
        public void WithoutTheCohesivePullARainContactFlightIsModel27s()
        {
            // The floor's belt_bare in rain: the flight rests in its wet groove, climbs out of it, lands and bounces on it.
            // Without the cohesive pull (σ_a 0 in every set) it is model 27's flight, every record and event; with it, model
            // 32's. The flight touches no straw, so the straw row's wet block plays no part.
            DroneSpec spec = Drone();
            SurfaceTable without = SoilScenarios.Table().Clone();
            for (int i = 0; i < without.Surfaces.Length; i++) without.Surfaces[i].Wet.Adhesion = without.Surfaces[i].Saturated.Adhesion = 0;
            foreach ((RainLevel rain, ulong parent, ulong pin) in new[] { (RainLevel.Rain, Model27ContactRainHash, ContactRainHash), (RainLevel.SevereRain, Model27ContactSevereHash, ContactSevereHash) })
            {
                var a = new System.Collections.Generic.List<double>();
                var b = new System.Collections.Generic.List<double>();
                double peak = 0;
                int pulling = 0, firstPull = -1, ea = 0, eb = 0;
                ulong with = GoldenFlights.FlyContact(spec, out FlightCore ca, rain: rain, after: core =>
                {
                    a.Add(core.State.Position.Y);
                    ea += core.Events.Count;
                    double pull = 0;
                    for (int c = 0; c < 4; c++) pull += core.Contacts.SoilOf(c).Pull;
                    if (pull > 0)
                    {
                        pulling++;
                        if (firstPull < 0) firstPull = a.Count;
                    }
                    peak = Math.Max(peak, pull);
                });
                ulong off = GoldenFlights.FlyContact(spec, out FlightCore cb, rain: rain, table: without, after: core => { b.Add(core.State.Position.Y); eb += core.Events.Count; });
                int first = -1, rose = -1, roseOff = -1;
                double most = 0;
                for (int i = 0; i < a.Count; i++)
                {
                    if (first < 0 && a[i] != b[i]) first = i + 1;
                    most = Math.Max(most, Math.Abs(a[i] - b[i]));
                    if (rose < 0 && a[i] > a[0] + 0.01) rose = i + 1;
                    if (roseOff < 0 && b[i] > b[0] + 0.01) roseOff = i + 1;
                }
                TestContext.WriteLine($"  {rain}: 0x{with:X16} (model 27 without the pull 0x{off:X16}); first differing step {first}, the first pull at step {firstPull}, "
                    + $"{pulling} steps pulling, the four grooves' pull at most {peak:F3} N; 10 mm up at step {rose} against {roseOff}; most apart {most * 1e3:F2} mm; "
                    + $"at rest {ca.State.Position.Y:F4} against {cb.State.Position.Y:F4} m; {ca.Contacts.ImpactCount} impacts against {cb.Contacts.ImpactCount}; "
                    + $"{ea} events against {eb}; crashed {ca.State.Crashed} {cb.State.Crashed}");
                Assert.That(off, Is.EqualTo(parent), rain + ": without the pull, model 27's flight");
                Assert.That(with, Is.EqualTo(pin), rain + ": model 32's");
                Assert.That(ca.State.Crashed, Is.False);
            }
        }
    }
}
