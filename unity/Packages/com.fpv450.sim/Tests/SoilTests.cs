using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// ground-contact's soil (p7a-ground 1.10, design 2 and 3) against published terramechanics: Bekker's linear
    /// pressure–sinkage (Bekker 1969, n = 1, k = k_c/b + k_φ) under Winkler's foundation, Wong's repetitive loading rule
    /// (Wong 2008, Theory of Ground Vehicles §2.4: unloading and reloading on a line of slope k_u above the loading's,
    /// leaving a plastic set), Coulomb friction with interface adhesion (τ = c_a + σ·tan δ, Potyondy 1961) and the
    /// passive resistance of the groove's wall. Every target also runs a known-wrong model that must fail it: the rigid
    /// ground with plain friction (the flight without surfaces.json, p6-flight-core 1.6's), and an elastic soil.
    /// </summary>
    public class SoilTests
    {
        const double R = 0.005, L = 0.22, Joint = 0.1025; // drone.json's skid tube (measured)

        static DroneSpec _drone;
        static DroneSpec Drone() => _drone ??= DroneSpec.Parse(System.IO.File.ReadAllText(ContactScenarios.RepositoryFile("assets-src/drone/drone.json")));

        static SoilSet Set(string surface, Moisture m = Moisture.Dry) => Array.Find(SoilScenarios.Table().Surfaces, s => s.Name == surface).Set(m);

        // The test's own section of the tube (Math, apart from Soil): the classic segment formula up to r, then the
        // rectangle beside the half-circle.
        static double Section(double z)
        {
            if (!(z > 0)) return 0;
            if (z >= R) return 19.0 / 12.0 * R * R + 2 * R * (z - R);
            double w = 2 * Math.Sqrt(2 * R * z - z * z);
            return 2.0 / 3.0 * w * z + z * z * z / (2 * w);
        }

        // The circular segment, exactly (Math.Acos).
        static double Segment(double z) => z >= R ? Math.PI * R * R / 2 + 2 * R * (z - R)
            : R * R * Math.Acos((R - z) / R) - (R - z) * Math.Sqrt(2 * R * z - z * z);

        // Wong's rule per column for a part whose displaced measure is a(z): virgin past zMax, else a at z* = zMax − ratio·(zMax − z).
        static double Wong(Func<double, double> a, double k, double ratio, double z, double zMax) =>
            z >= zMax ? k * a(z) : k * a(zMax - ratio * (zMax - z));

        // ---- The approximations are bounded ------------------------------------------------------------------------

        [Test]
        public void TheSectionIsTheCircularSegmentWithinItsBound()
        {
            double worst = 0, at = 0;
            for (int i = 1; i <= 1000; i++)
            {
                double z = R * i / 1000, e = Math.Abs(Section(z) / Segment(z) - 1);
                Assert.That(Soil.Displaced(SoilPart.Tube, R, z, out _, out _), Is.EqualTo(Section(z)).Within(1e-12).Percent, "Soil's section is the formula");
                if (e > worst)
                {
                    worst = e;
                    at = z;
                }
            }
            double half = Math.Abs(Section(R / 2) / Segment(R / 2) - 1), parabola = Math.Abs(2.0 / 3.0 * 2 * R * R / Segment(R) - 1);
            TestContext.WriteLine($"the section against the circular segment up to r: worst {worst * 100:F2} % at z = {at * 1e3:F3} mm, {half * 100:F3} % at r/2; "
                + $"design 2's parabola alone {parabola * 100:F1} % at r");
            Assert.That(worst, Is.LessThan(0.15), "within 15 % up to the tube's radius");
            Assert.That(worst, Is.LessThan(0.01), "the classic formula holds it under 1 %");
        }

        [Test]
        public void TheThreePointRuleIsWithinOnePercentOfThirtyTwoPoints()
        {
            // At each surface's resting sink (a corner's 11.85 N, every moisture) and tilts up to the rests' and past them:
            // the half-skid's force by the model's three nodes against a 32-interval composite Simpson rule.
            double worst = 0;
            string where = "";
            foreach (SurfaceRow row in SoilScenarios.Table().Surfaces)
            {
                if (row.Dry.Rigid) continue;
                foreach (Moisture m in new[] { Moisture.Dry, Moisture.Wet, Moisture.Saturated })
                {
                    SoilSet set = row.Set(m);
                    double k = Soil.Modulus(set, 2 * R);
                    double z = SoilScenarios.LevelSink(set, R, L, 11.85, SoilScenarios.Table().FirmLayer);
                    foreach (double tilt in new[] { 0, 0.05e-3, 0.2e-3 }) // the rests' tilts (up to 0.22 mm over the half-skid saturated)
                    {
                        Func<double, double> sink = s => z + tilt * (s / L - 0.5);
                        double w1 = L * L * L / (6 * Joint * (L - Joint)), w2 = L / 2 - L * L / (6 * (L - Joint)), w0 = L - w1 - w2;
                        double three = k * (w0 * Section(sink(0)) + w1 * Section(sink(Joint)) + w2 * Section(sink(L)));
                        double fine = 0;
                        for (int i = 0; i <= 32; i++)
                            fine += (i == 0 || i == 32 ? 1 : i % 2 == 1 ? 4 : 2) * Section(sink(L * i / 32));
                        fine *= k * L / 96;
                        double e = Math.Abs(three / fine - 1);
                        if (e > worst)
                        {
                            worst = e;
                            where = $"{row.Name} {m} at {z * 1e3:F2} mm, tilt {tilt * 1e3:F1} mm";
                        }
                    }
                }
            }
            TestContext.WriteLine($"the three-point rule against 32 intervals: worst {worst * 100:F4} % ({where})");
            Assert.That(worst, Is.LessThan(0.01));
        }

        // ---- A resting drone sinks as the rows say -------------------------------------------------------------------

        static void AssertRest(SoilScenarios.Rest r, string name, SoilSet set)
        {
            Assert.That(r.LateEvents, Is.EqualTo(0), name + ": no contact event in the last second");
            for (int c = 0; c < 4; c++)
            {
                ContactModel.SkidSoil k = r.Skids[c];
                Assert.That(k.Engaged, Is.True, name + " corner " + c + " rests in soil");
                Assert.That(Math.Abs(k.Residual), Is.LessThan(1e-9), name + ": the leg's k·x equals the soil's force");
                // The sink at the load centroid, and what the row derives for the corner's load: the virgin sink, carried
                // onto Wong's line from the deepest sink the settling reached there.
                double t = k.Centroid;
                double sink = t <= Joint ? k.Sink0 + (k.Sink1 - k.Sink0) * t / Joint : k.Sink1 + (k.Sink2 - k.Sink1) * (t - Joint) / (L - Joint);
                double max = t <= Joint ? k.Max0 + (k.Max1 - k.Max0) * t / Joint : k.Max1 + (k.Max2 - k.Max1) * (t - Joint) / (L - Joint);
                double virgin = SoilScenarios.LevelSink(set, R, L, k.Static, SoilScenarios.Table().FirmLayer);
                double derived = max > virgin ? max - (max - virgin) / set.UnloadRatio : virgin;
                TestContext.WriteLine($"  {name} corner {c}: {k.Static:F3} N, the leg {k.Deflection * 1e3:F3} mm, the centroid {t:F4} m out sinks {sink * 1e3:F4} mm "
                    + $"(derived {derived * 1e3:F4}: virgin {virgin * 1e3:F4}, deepest {max * 1e3:F4}), residual {k.Residual:E1} N");
                Assert.That(sink, Is.EqualTo(derived).Within(2).Percent, name + " corner " + c);
            }
        }

        [Test]
        public void ARestingDroneSinksAsTheRowsSay()
        {
            DroneSpec spec = Drone();
            foreach (SurfaceRow row in SoilScenarios.Table().Surfaces)
            {
                if (row.Dry.Rigid) continue;
                AssertRest(SoilScenarios.Resting(spec, SoilScenarios.Floor(row.Name), default), row.Name + " (the stub's floor)", row.Dry);
            }
            // The test range's own ground, the plane's belt_straw at the track start.
            var range = new FlightWorld(SoilScenarios.Range());
            AssertRest(SoilScenarios.Resting(spec, range, SoilScenarios.TrackStart(range)), "the range's track start", Set("belt_straw"));

            // Known-wrong: the rigid ground with plain friction sinks nothing.
            SoilScenarios.Rest rigid = SoilScenarios.Resting(spec, SoilScenarios.Floor("belt_straw"), default, rigid: true);
            Assert.That(rigid.Skids[0].Engaged, Is.False, "the rigid ground has no soil to sink into, so it fails the rows' sinks");
        }

        [Test]
        public void RainSinksTheSkidsDeeper()
        {
            DroneSpec spec = Drone();
            foreach (string name in new[] { "dirt_track", "belt_straw", "tilled", "crater_spoil" })
            {
                double last = 0;
                foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain, RainLevel.SevereRain })
                {
                    SoilScenarios.Rest r = SoilScenarios.Resting(spec, SoilScenarios.Floor(name), default, rain);
                    double sink = 0;
                    for (int c = 0; c < 4; c++) sink = Math.Max(sink, r.Skids[c].Sink1);
                    TestContext.WriteLine($"  {name} {rain}: {sink * 1e3:F3} mm at the T-joints");
                    Assert.That(sink, Is.GreaterThan(last), name + ": each wetter set sinks deeper");
                    last = sink;
                }
            }
        }

        // ---- Soil keeps a plastic set --------------------------------------------------------------------------------

        // A part pressed to a force F three times in one print and lifted off between: its sinks at the force (on), the
        // sets it leaves (off) and its reloading stiffness at the set, by Soil alone; first, the first loading's stiffness.
        static void Cycle(SoilPart part, double r, double length, in SoilSet set, double force, out double[] on, out double[] off, out double[] reload, out double first)
        {
            double k = Soil.Modulus(set, Soil.Width(part, r)), firm = SoilScenarios.Table().FirmLayer, max = 0;
            on = new double[3];
            off = new double[3];
            reload = new double[3];
            Soil.Force(part, r, set, k, firm, 1e-6, 0, out first);
            first *= length;
            for (int n = 0; n < 3; n++)
            {
                double lo = 0, hi = 0.2;
                for (int i = 0; i < 200; i++)
                {
                    double z = 0.5 * (lo + hi);
                    if (Soil.Force(part, r, set, k, firm, z, max, out _) * length > force) hi = z;
                    else lo = z;
                }
                on[n] = 0.5 * (lo + hi);
                max = Math.Max(max, on[n]);
                off[n] = Soil.Set(set, max);
                Soil.Force(part, r, set, k, firm, off[n] + 1e-7, max, out reload[n]);
                reload[n] *= length;
            }
        }

        [Test]
        public void SoilKeepsAPlasticSet()
        {
            SoilSet spoil = Set("crater_spoil");
            // The 15 mm reference disc at 25 N: Wong's flat plate, reloading on k_u = ratio × k_l.
            Cycle(SoilPart.Disc, 0.0075, 1, spoil, 25, out double[] on, out double[] off, out double[] reload, out double first);
            for (int n = 0; n < 3; n++)
            {
                TestContext.WriteLine($"  disc, loading {n + 1}: {on[n] * 1e3:F3} mm at 25 N, the set {off[n] * 1e3:F3} mm, reloading at {reload[n]:F1} N/m, "
                    + $"{reload[n] / first:F9} × the first loading's {first:F1}");
                Assert.That(on[n] - off[n], Is.LessThan(on[n]), "the sink recovered is smaller than the sink applied");
                Assert.That(reload[n] / first, Is.EqualTo(spoil.UnloadRatio).Within(1e-9 * spoil.UnloadRatio));
            }
            Assert.That(off[0], Is.GreaterThan(0), "the set grows on the first loading");
            Assert.That(off[2] - off[1], Is.LessThan(0.05 * off[0]), "and by less than 5 % of it on the third");

            // The half-skid: its unloading and reloading are Wong's line column by column, within 1e-9 relative.
            double k = Soil.Modulus(spoil, 2 * R), firm = SoilScenarios.Table().FirmLayer;
            Cycle(SoilPart.Tube, R, L, spoil, 12, out on, out off, out _, out _);
            double zm = on[0];
            for (int i = 1; i < 20; i++)
            {
                double z = off[0] + (zm - off[0]) * i / 20;
                double model = Soil.Force(SoilPart.Tube, R, spoil, k, firm, z, zm, out _), wong = Wong(Section, k, spoil.UnloadRatio, z, zm);
                Assert.That(model, Is.EqualTo(wong).Within(1e-9 * wong + 1e-15), "the half-skid follows its groove's line at " + z);
            }
            TestContext.WriteLine($"  half-skid: {on[0] * 1e3:F3} mm at 12 N, the set {off[0] * 1e3:F3} mm (z_m·(1 − 1/{spoil.UnloadRatio})), "
                + $"then {on[1] * 1e3:F3} and {on[2] * 1e3:F3} mm, sets {off[1] * 1e3:F3} and {off[2] * 1e3:F3} mm");
            Assert.That(off[0], Is.GreaterThan(0));
            Assert.That(off[2] - off[1], Is.LessThan(0.05 * off[0]));

            // Known-wrong: an elastic soil (unload ratio 1) keeps no set, so it fails.
            SoilSet elastic = spoil;
            elastic.UnloadRatio = 1;
            Cycle(SoilPart.Disc, 0.0075, 1, elastic, 25, out _, out off, out _, out _);
            Assert.That(off[0], Is.EqualTo(0), "the elastic soil keeps no set");
            Cycle(SoilPart.Tube, R, L, elastic, 12, out _, out off, out _, out _);
            Assert.That(off[0], Is.EqualTo(0), "the elastic soil keeps no set under the tube either (Wong's rule per column)");
        }

        [Test]
        public void ASphereDisplacesItsCap()
        {
            // The cap's published geometry, V = πz²(r − z/3), its rate the base πa², and past the equator a cylinder.
            const double r = 0.0415; // the coil's end (drone.json)
            foreach (double z in new[] { 0.001, 0.01, 0.03, r, 0.06 })
            {
                double v = Soil.Displaced(SoilPart.Sphere, r, z, out double rate, out double foot);
                double cap = z <= r ? Math.PI * z * z * (r - z / 3) : 2.0 / 3.0 * Math.PI * r * r * r + Math.PI * r * r * (z - r);
                double basis = z <= r ? Math.PI * (2 * r * z - z * z) : Math.PI * r * r;
                Assert.That(v, Is.EqualTo(cap).Within(1e-12).Percent);
                Assert.That(rate, Is.EqualTo(basis).Within(1e-12).Percent);
                Assert.That(foot, Is.EqualTo(basis).Within(1e-12).Percent);
            }
        }

        // ---- A sunk skid resists sideways more than along ------------------------------------------------------------

        [Test]
        public void ASunkSkidResistsSidewaysMoreThanAlong()
        {
            DroneSpec spec = Drone();
            var across = new Double3(1, 0, 0);
            var along = new Double3(0, 0, 1);
            SoilScenarios.Push a = SoilScenarios.Drag(spec, SoilScenarios.Floor("belt_straw"), 0.003, across);
            SoilScenarios.Push b = SoilScenarios.Drag(spec, SoilScenarios.Floor("belt_straw"), 0.003, along);
            double ratio = a.Breakaway / b.Breakaway, derived = a.DerivedHold / b.DerivedHold;
            double friction = 0.5 * a.Normal;
            TestContext.WriteLine($"  belt_straw dry, the skids {a.Sink * 1e3:F2} mm in, {a.Normal:F2} N on the ground: across breaks at {a.Breakaway:F3} N "
                + $"(derived {a.DerivedHold:F3}) and slides at {a.Sliding:F3} (derived {a.DerivedSlide:F3}); along at {b.Breakaway:F3} N (derived {b.DerivedHold:F3}), "
                + $"slides at {b.Sliding:F3}; across/along {ratio:F4}, derived {derived:F4}; the groove's own share {(a.DerivedHold - friction) / (b.DerivedHold - 0.5 * b.Normal):F2}×");
            Assert.That(a.Breakaway, Is.EqualTo(a.DerivedHold).Within(2).Percent, "across breaks at μs·Fn + P(90°)");
            Assert.That(b.Breakaway, Is.EqualTo(b.DerivedHold).Within(2).Percent, "along breaks at μs·Fn + P(0°)");
            Assert.That(ratio, Is.EqualTo(derived).Within(2).Percent);
            Assert.That(ratio, Is.GreaterThan(1.05));
            Assert.That(a.Sliding, Is.EqualTo(a.DerivedSlide).Within(2).Percent, "and slides at μk·Fn + P");

            // On a rigid floor (no groove) both break at μs·Fn, found by bisection on the push; it is the known-wrong
            // model, and fails the ratio.
            double c = SoilScenarios.Breakaway(spec, SoilScenarios.Floor("belt_straw"), 0.003, across, out double cn, rigid: true);
            double d = SoilScenarios.Breakaway(spec, SoilScenarios.Floor("belt_straw"), 0.003, along, out double dn, rigid: true);
            TestContext.WriteLine($"  rigid floor: across {c:F12} N, along {d:F12} N against μs·Fn {0.5 * cn:F12} and {0.5 * dn:F12}");
            Assert.That(c, Is.EqualTo(0.5 * cn).Within(1e-9));
            Assert.That(d, Is.EqualTo(0.5 * dn).Within(1e-9));
            Assert.That(c / d, Is.Not.EqualTo(derived).Within(2).Percent, "the rigid ground fails the ratio");
        }

        [Test]
        public void AWetGrooveHoldsByItsInterfaceAdhesion()
        {
            // In Rain the anchor's limit gains c_a·S_m (Coulomb with interface adhesion, τ = c_a + σ·tan δ): the wet skid
            // breaks at μs·Fn + c_a·S_m + P and slides at μk·Fn + c_a·S_m + P; the rigid ground holds only μs·Fn.
            DroneSpec spec = Drone();
            SoilScenarios.Push a = SoilScenarios.Drag(spec, SoilScenarios.Floor("belt_straw"), 0.008, new Double3(1, 0, 0), RainLevel.Rain);
            SoilSet wet = Set("belt_straw", Moisture.Wet);
            TestContext.WriteLine($"  belt_straw wet, {a.Sink * 1e3:F2} mm in, {a.Normal:F2} N: breaks at {a.Breakaway:F3} N (derived {a.DerivedHold:F3}, of it μs·Fn "
                + $"{wet.StaticFriction * a.Normal:F3}), slides at {a.Sliding:F3} (derived {a.DerivedSlide:F3})");
            Assert.That(a.Breakaway, Is.EqualTo(a.DerivedHold).Within(2).Percent);
            Assert.That(a.Sliding, Is.EqualTo(a.DerivedSlide).Within(2).Percent);
            Assert.That(a.Breakaway, Is.GreaterThan(wet.StaticFriction * a.Normal * 1.1), "adhesion and the groove hold more than friction");
        }

        // ---- Loose soil swallows a fast landing -------------------------------------------------------------------------

        [Test]
        public void LooseSoilSwallowsAFastLanding()
        {
            DroneSpec spec = Drone();
            SoilScenarios.Landing spoil = SoilScenarios.Land(spec, SoilScenarios.Floor("crater_spoil"), default, 1.5);
            SoilScenarios.Landing track = SoilScenarios.Land(spec, SoilScenarios.Floor("dirt_track"), default, 1.5);
            SoilScenarios.Landing spoilElastic = SoilScenarios.Land(spec, SoilScenarios.Floor("crater_spoil"), default, 1.5,
                table: SoilScenarios.With("crater_spoil", x => { x.UnloadRatio = 1; return x; }));
            SoilScenarios.Landing trackElastic = SoilScenarios.Land(spec, SoilScenarios.Floor("dirt_track"), default, 1.5,
                table: SoilScenarios.With("dirt_track", x => { x.UnloadRatio = 1; return x; }));
            foreach ((string n, SoilScenarios.Landing l) in new[] { ("crater_spoil", spoil), ("dirt_track", track), ("crater_spoil elastic", spoilElastic), ("dirt_track elastic", trackElastic) })
                TestContext.WriteLine($"  {n}: touch {l.TouchSpeed:F3} m/s, sank {l.PeakSink * 1e3:F2} mm, rests {l.RestSink * 1e3:F2} mm, set {l.Set * 1e3:F2} mm, "
                    + $"stopped in {l.Stop * 1e3:F1} ms at {l.PeakAccel:F1} m/s² peak, rose {l.Rebound * 1e3:F2} mm again; {l.Tunnels} tunnels, {l.Crashes} crashes");
            Assert.That(spoil.PeakSink, Is.GreaterThan(track.PeakSink), "crater_spoil swallows more");
            Assert.That(spoil.Stop, Is.GreaterThan(track.Stop), "the stop takes longer");
            Assert.That(spoil.PeakAccel, Is.LessThan(track.PeakAccel), "at a lower peak");
            Assert.That(spoil.Set, Is.GreaterThan(0), "and a set remains");
            Assert.That(track.Set, Is.GreaterThan(0));
            // Known-wrong: the elastic soil keeps no set and returns more of the landing on both, so it fails.
            Assert.That(spoilElastic.Set, Is.EqualTo(0));
            Assert.That(trackElastic.Set, Is.EqualTo(0));
            Assert.That(spoilElastic.Rebound, Is.GreaterThan(spoil.Rebound));
            Assert.That(trackElastic.Rebound, Is.GreaterThan(track.Rebound));
            Assert.That(spoil.Tunnels + spoil.Crashes + track.Tunnels + track.Crashes, Is.EqualTo(0));
        }

        // ---- A re-landing finds its prints ---------------------------------------------------------------------------

        [Test]
        public void AReLandingFindsItsPrints()
        {
            SoilScenarios.Relanding r = SoilScenarios.Relandings(Drone(), SoilScenarios.Floor("crater_spoil"), default);
            for (int i = 0; i < 3; i++)
                TestContext.WriteLine($"  landing {i + 1}{(i == 2 ? " (0.3 m aside)" : "")}: touch {r.Touch[i]:F3} m/s, the soil yields {r.Yield[i] * 1e3:F3} mm "
                    + $"(from a floor {r.Floor[i] * 1e3:F3} mm down, a print {(r.Print[i] ? "found" : "not found")})");
            Assert.That(r.Print[0], Is.False);
            Assert.That(r.Print[1], Is.True, "the second landing finds its print");
            Assert.That(r.Yield[1], Is.LessThan(r.Yield[0]), "and sinks less than the first");
            Assert.That(r.Print[2], Is.False);
            Assert.That(r.Yield[2], Is.EqualTo(r.Yield[0]).Within(2).Percent, "the landing aside sinks as the first did");

            // Known-wrong: the rigid ground has no prints and no yield, so it fails.
            SoilScenarios.Relanding rigid = SoilScenarios.Relandings(Drone(), SoilScenarios.Floor("crater_spoil"), default, rigid: true);
            Assert.That(rigid.Yield[1] < rigid.Yield[0], Is.False, "the rigid ground's re-landing sinks no less");
        }

        // ---- The pilot's liftoff and glide, graded by their ordering ----------------------------------------------------

        [Test]
        public void NoLegIsHeldOnTheDryTrack()
        {
            // Design 13's held leg on the dry track at four seeds (the motors' asymmetry drawn): none here.
            DroneSpec spec = Drone();
            double hover = SoilScenarios.HoverThrottle(spec, SoilScenarios.Floor("dirt_track"), default);
            for (ulong seed = 1; seed <= 4; seed++)
            {
                SoilScenarios.Liftoff l = SoilScenarios.Lift(spec, SoilScenarios.Floor("dirt_track"), default, hover, RainLevel.Sunny, seed, true);
                TestContext.WriteLine($"  seed {seed}: the last corner left {l.Hold * 1e3:F1} ms after the others, the drone turned {l.Turn:F2}° toward it");
                Assert.That(l.Held, Is.EqualTo(-1));
            }
        }

        [Test]
        public void TheGlideLandingPitchesFurtherOnSoftGround()
        {
            DroneSpec spec = Drone();
            SoilScenarios.Landing tilled = SoilScenarios.Land(spec, SoilScenarios.Floor("tilled"), default, 0.5, 0.3);
            SoilScenarios.Landing track = SoilScenarios.Land(spec, SoilScenarios.Floor("dirt_track"), default, 0.5, 0.3);
            SoilScenarios.Landing rigidTilled = SoilScenarios.Land(spec, SoilScenarios.Floor("tilled"), default, 0.5, 0.3, rigid: true);
            SoilScenarios.Landing rigidTrack = SoilScenarios.Land(spec, SoilScenarios.Floor("dirt_track"), default, 0.5, 0.3, rigid: true);
            TestContext.WriteLine($"  the glide: tilled {tilled.NoseDown:F3}° nose down ({tilled.PeakSink * 1e3:F2} mm), dirt track {track.NoseDown:F3}° "
                + $"({track.PeakSink * 1e3:F2} mm); rigid ground {rigidTilled.NoseDown:F3}° and {rigidTrack.NoseDown:F3}°");
            Assert.That(tilled.NoseDown, Is.GreaterThan(track.NoseDown));
            Assert.That(Math.Abs(rigidTilled.NoseDown - rigidTrack.NoseDown), Is.LessThan(0.5 * (tilled.NoseDown - track.NoseDown)),
                "the rigid ground with plain friction cannot tell them apart, so it fails");
        }

        [Test, Explicit]
        public void AuditTheContactGolden()
        {
            DroneSpec spec = Drone();
            var a = new System.Collections.Generic.List<(double y, int touching, double load)>();
            var b = new System.Collections.Generic.List<(double y, int touching, double load)>();
            int ea = 0, eb = 0;
            GoldenFlights.FlyContact(spec, out FlightCore ca, after: c => { a.Add((c.State.Position.Y, c.Trace.ContactsTouching, c.Trace.LegLoad[0])); ea += c.Events.Count; });
            GoldenFlights.FlyContact(spec, out FlightCore cb, after: c => { b.Add((c.State.Position.Y, c.Trace.ContactsTouching, c.Trace.LegLoad[0])); eb += c.Events.Count; }, soil: false);
            int first = -1;
            double most = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (first < 0 && a[i].y != b[i].y) first = i;
                most = Math.Max(most, Math.Abs(a[i].y - b[i].y));
            }
            TestContext.WriteLine($"first differing step {first + 1}; most apart {most * 1e3:F2} mm; events {ea} vs {eb}; impacts {ca.Contacts.ImpactCount} vs {cb.Contacts.ImpactCount}; "
                + $"rest {ca.State.Position.Y:F4} vs {cb.State.Position.Y:F4} m; at 1 s {a[999].y:F4} vs {b[999].y:F4}; crashed {ca.State.Crashed} {cb.State.Crashed}");
            for (int c = 0; c < 4; c++)
            {
                var k = ca.Contacts.SoilOf(c);
                TestContext.WriteLine($"  corner {c}: sinks {k.Sink0 * 1e3:F3} {k.Sink1 * 1e3:F3} {k.Sink2 * 1e3:F3} mm, deepest {k.Max1 * 1e3:F3}");
            }
            // the held bounce's top: the highest after the skids first came down below 0.1 m
            double topA = 0, topB = 0;
            for (int i = 7000; i < a.Count; i++) { topA = Math.Max(topA, a[i].y); topB = Math.Max(topB, b[i].y); }
            TestContext.WriteLine($"highest after step 7000: {topA:F4} vs {topB:F4}");
        }

        // ---- Determinism, replay and allocations ---------------------------------------------------------------------

        [Test]
        public void ASoilFlightOnTheRangeReplaysBitForBit()
        {
            // The liftoff ramp from the range's track start in Rain, its half-skids in the plane's wet belt_straw, recorded
            // with its header (the table, the rain) and replayed headless on the same world: every step bit for bit.
            DroneSpec spec = Drone();
            var world = new FlightWorld(SoilScenarios.Range());
            FlightSetup setup = SoilScenarios.Setup(spec, world, SoilScenarios.TrackStart(world) + new Double3(0, SoilScenarios.Standing(spec), 0), RainLevel.Rain, 7, true);
            var core = new FlightCore(setup);
            Log.LogHeader header = Log.LogHeader.For(setup, core);
            header.WorldId = "test-range";
            header.SetDrone(spec, spec.Nominal);
            var stream = new System.IO.MemoryStream();
            var recorder = new Log.FlightRecorder(stream, header, Log.FlightRecorder.DefaultCapacity, ownsStream: false);
            bool engaged = false;
            for (int k = 0; k < 4000; k++)
            {
                var stick = new Control.StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 8, Throttle = k < 2000 ? 0 : Math.Min(1, (k - 2000) / 500.0) * 0.58 };
                core.Advance(stick);
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
                engaged |= core.Contacts.SoilOf(0).Engaged;
            }
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            Assert.That(engaged, Is.True, "the flight rested in the soil");
            Assert.That(core.State.Position.Y, Is.GreaterThan(SoilScenarios.Standing(spec) + 0.1), "and lifted off it");
            ulong table = SoilScenarios.Table().Hash;
            Log.ReplayResult r = Log.FlightReplay.Run(new System.IO.MemoryStream(stream.ToArray()), id => id == "test-range" ? world : null, spec.ContentHash, null, table);
            TestContext.WriteLine($"  replay: {r.Outcome}, {r.Steps} steps; {r.Message}");
            Assert.That(r.Outcome, Is.EqualTo(Log.ReplayOutcome.Match), r.Message);
            Assert.That(r.Steps, Is.EqualTo(4000));
        }

        [Test]
        public void ASoilFlightIsTheSameTwiceAndAllocatesNothing()
        {
            DroneSpec spec = Drone();
            var x = new FlightCore(SoilScenarios.Setup(spec, SoilScenarios.Floor("tilled"), new Double3(0, SoilScenarios.Standing(spec), 0), RainLevel.Rain));
            var y = new FlightCore(SoilScenarios.Setup(spec, SoilScenarios.Floor("tilled"), new Double3(0, SoilScenarios.Standing(spec), 0), RainLevel.Rain));
            for (int k = 0; k < 500; k++)
            {
                x.Advance(new Control.StickSample { HostUs = k * FlightCore.StepUs });
                y.Advance(new Control.StickSample { HostUs = k * FlightCore.StepUs });
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 500; k < 1500; k++) x.Advance(new Control.StickSample { HostUs = k * FlightCore.StepUs });
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            for (int k = 500; k < 1500; k++) y.Advance(new Control.StickSample { HostUs = k * FlightCore.StepUs });
            Assert.That(allocated, Is.EqualTo(0), "0 B a step on soil");
            Assert.That(BitConverter.DoubleToInt64Bits(x.State.Position.Y), Is.EqualTo(BitConverter.DoubleToInt64Bits(y.State.Position.Y)));
            Assert.That(x.Contacts.SoilOf(0).Engaged, Is.True);
        }
    }
}
