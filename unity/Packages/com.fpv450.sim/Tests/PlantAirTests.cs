using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The plants in the air (w16-plants-in-the-air 1.2, design.md): the classes, a gust's bend and return, the two regimes and the
    /// closed form in the flight's field, each against tools/flightbench/handcalc/plants_air.py (independent arithmetic: the full
    /// law with the drag on the air relative to the moving stem, RK4 at 0.1 ms) or against the field's own time-domain air.
    /// </summary>
    public class PlantAirTests
    {
        const double Rho = 1.225; // tether.json's fiber.air_density_kg_m3, the plants' air

        static void Print(string line) => TestContext.Out.WriteLine(line);

        static CoverSet Cover(CoverKind kind) => SoilScenarios.Table().Cover[(int)kind].Dry;

        static double LeafArea => SoilScenarios.Table().Cover[(int)CoverKind.Leaf].Dry.MassPerArea;

        internal static PlantMechanics Green(double l) => PlantMechanics.Stem(PlantKind.GreenCulm, l, Cover(CoverKind.GrassBlade), Rho);
        internal static PlantMechanics Dry(double l) => PlantMechanics.Stem(PlantKind.DryCulm, l, Cover(CoverKind.DryCulm), Rho);
        static PlantMechanics Weed(double l) => PlantMechanics.Stem(PlantKind.WeedStalk, l, Cover(CoverKind.Stem), Rho);
        static PlantMechanics Twig() => PlantMechanics.Woody(PlantKind.LeafyTwig, 0.5, 0.005, 0.02, Cover(CoverKind.Twig), LeafArea, Rho);
        static PlantMechanics Branch() => PlantMechanics.Woody(PlantKind.Branch, 3.5, 0.04, 1.2, Cover(CoverKind.Twig), LeafArea, Rho);

        // plants_air.py's class table: k (N·m/rad), I (kg·m²), f with its weight (Hz), ζ_air upright at 2, 5 and 10 m/s.
        static readonly (string Name, Func<PlantMechanics> Make, double K, double I, double F, double Z2, double Z5, double Z10)[] Classes =
        {
            ("green culm 0.67 m", () => Green(0.67), 4.8299e-03, 6.4916e-05, 1.145, 0.421, 1.053, 2.106),
            ("dry culm 0.9 m", () => Dry(0.9), 9.2039e-02, 5.6963e-04, 1.915, 0.209, 0.523, 1.047),
            ("weed stalk 0.5 m", () => Weed(0.5), 5.7157e-01, 8.4146e-04, 4.054, 0.014, 0.035, 0.070),
            ("leafy twig 5 mm x 0.5 m", Twig, 2.4298e+00, 1.5637e-03, 6.212, 0.030, 0.037, 0.049),
            ("branch 40 mm x 3.5 m", Branch, 1.4218e+03, 3.3216e+01, 0.987, 0.157, 0.163, 0.172),
        };

        // The air's damping ratio at uniform air U across the upright link (the law's own damping integral).
        static double ZetaAir(in PlantMechanics m, double u)
        {
            PlantLaw.Uniform(m, 0, 1, 0, new Double3(u, 0, 0), out PlantLaw.Forces f);
            return f.DampAlong / (2 * m.Inertia * PlantLaw_Omega(m));
        }

        static double PlantLaw_Omega(in PlantMechanics m) => 2 * Math.PI * m.Frequency;

        [Test]
        public void TheClassesAreTheHandCalculations()
        {
            foreach (var c in Classes)
            {
                PlantMechanics m = c.Make();
                double z2 = ZetaAir(m, 2), z5 = ZetaAir(m, 5), z10 = ZetaAir(m, 10);
                Print($"{c.Name}: k {m.Stiffness:E4} N·m/rad (hand {c.K:E4}), I {m.Inertia:E4} kg·m² (hand {c.I:E4}), f {m.Frequency:F3} Hz "
                    + $"(hand {c.F:F3}), ζ_air at 2/5/10 m/s {z2:F3}/{z5:F3}/{z10:F3} (hand {c.Z2:F3}/{c.Z5:F3}/{c.Z10:F3}), ζ_s {m.Damping / (2 * m.Inertia * PlantLaw_Omega(m)):F3}");
                Assert.That(m.Stiffness, Is.EqualTo(c.K).Within(c.K * 1e-4), c.Name + ": k");
                Assert.That(m.Inertia, Is.EqualTo(c.I).Within(c.I * 1e-4), c.Name + ": I");
                Assert.That(m.Frequency, Is.EqualTo(c.F).Within(1e-3), c.Name + ": f");
                Assert.That(z2, Is.EqualTo(c.Z2).Within(1e-3), c.Name + ": ζ_air at 2 m/s");
                Assert.That(z5, Is.EqualTo(c.Z5).Within(1e-3), c.Name + ": ζ_air at 5 m/s");
                Assert.That(z10, Is.EqualTo(c.Z10).Within(1e-3), c.Name + ": ζ_air at 10 m/s");
            }
        }

        // The two regimes (design.md §1.3): a grass culm is overdamped by the air from a few m/s, a twig and a branch ring at any
        // wind the field blows (ζ = ζ_s + ζ_a under 0.3 at 10 m/s).
        [Test]
        public void GrassIsOverdampedByTheAirAndTwigsRing()
        {
            PlantMechanics culm = Green(0.67), twig = Twig(), branch = Branch(), dry = Dry(0.2);
            double zs = PlantLaw.StemZeta;
            Assert.That(zs + ZetaAir(culm, 2), Is.LessThan(1), "a culm in 2 m/s: under critical");
            Assert.That(zs + ZetaAir(culm, 5), Is.GreaterThan(1), "a culm in 5 m/s: overdamped");
            Assert.That(PlantLaw.CrownZeta + ZetaAir(twig, 10), Is.LessThan(0.3), "a twig rings in 10 m/s");
            Assert.That(PlantLaw.CrownZeta + ZetaAir(branch, 10), Is.LessThan(0.3), "a branch rings in 10 m/s");
            Assert.That(dry.Frequency, Is.GreaterThan(14), "stubble (0.2 m) is stiff: 15 Hz");
            // The leaning culm sees less of the air across it, so in 5 m/s its own damping at its lean (0.79 rad) is under critical.
            var at = new Double3(5, 0, 0);
            PlantLaw.Uniform(culm, 0.785, 1, 0, at, out PlantLaw.Forces lean);
            double zLean = zs + lean.DampAlong / (2 * culm.Inertia * 2 * Math.PI * culm.Frequency);
            Print($"ζ of a 0.67 m green culm: {zs + ZetaAir(culm, 2):F3} at 2 m/s, {zs + ZetaAir(culm, 5):F3} at 5 m/s upright, {zLean:F3} at 5 m/s leaning 0.785 rad; "
                + $"a twig {PlantLaw.CrownZeta + ZetaAir(twig, 10):F3}, a branch {PlantLaw.CrownZeta + ZetaAir(branch, 10):F3} at 10 m/s");
            Assert.That(zLean, Is.LessThan(1), "leaning, the culm's air damping falls under critical");
        }

        // plants_air.py's gusts: uniform air u0 → u1 for 1 s → u0, from rest at u0's lean; the lean (rad) and its rate (rad/s) at
        // 0.1, 0.25, 0.5, 1.0, 1.25, 1.5, 2.0 and 3.0 s.
        static readonly double[] GustTimes = { 0.1, 0.25, 0.5, 1.0, 1.25, 1.5, 2.0, 3.0 };

        static readonly (string Name, Func<PlantMechanics> Make, double U0, double U1, double[] Lean, double[] Rate)[] Gusts =
        {
            ("green culm 0.67 m, 5 → 9 → 5 m/s", () => Green(0.67), 5, 9,
                new[] { 1.013563, 1.144378, 1.076639, 1.082475, 0.746529, 0.785376, 0.784412, 0.785158 },
                new[] { 2.745458, -0.700270, 0.185108, 0.001008, -0.406021, 0.169451, 0.008628, -0.000025 }),
            ("green culm 0.67 m, 2 → 6 → 2 m/s", () => Green(0.67), 2, 6,
                new[] { 0.538431, 0.999876, 0.871712, 0.888408, 0.328075, 0.149528, 0.263026, 0.245924 },
                new[] { 4.296022, 1.010123, -0.287734, -0.017743, -1.989784, 0.206989, -0.071720, -0.003539 }),
            ("branch 40 mm × 3.5 m, 2 → 6 → 2 m/s", Branch, 2, 6,
                new[] { 0.040193, 0.107162, 0.178709, 0.109327, 0.038713, -0.013985, 0.038320, 0.024698 },
                new[] { 0.340830, 0.475640, 0.036744, -0.032505, -0.362033, -0.025610, 0.026058, 0.012043 }),
        };

        // The gust by the law's step (dt), the samples at GustTimes.
        static void Gust(in PlantMechanics m, double u0, double u1, double dt, double[] lean, double[] rate)
        {
            var s = new PlantState();
            for (int k = 0; k < (int)(20 / dt); k++) PlantLaw.Step(m, ref s, 0, 0, new Double3(u0, 0, 0), dt);
            int next = 0;
            for (long k = 0; next < GustTimes.Length; k++)
            {
                double t = k * dt;
                if (t >= GustTimes[next] - 1e-9)
                {
                    lean[next] = s.X;
                    rate[next] = s.RateX;
                    next++;
                }
                PlantLaw.Step(m, ref s, 0, 0, new Double3(t < 1 - 1e-9 ? u1 : u0, 0, 0), dt);
            }
        }

        // The gust as the wash grid steps a stand: the air held over each 40 ms tick, PlantWash.SubSteps steps in it, sampled at the
        // first sub-step's end at or after each time (up to 10 ms late).
        static void GustTicked(in PlantMechanics m, double u0, double u1, double[] lean, double[] rate)
        {
            const double tick = 0.04;
            double dt = tick / PlantWash.SubSteps;
            var s = new PlantState();
            for (int k = 0; k < (int)(20 / dt); k++) PlantLaw.Step(m, ref s, 0, 0, new Double3(u0, 0, 0), dt);
            int next = 0;
            for (long k = 0; next < GustTimes.Length; k++)
            {
                double t = k * dt;
                if (t >= GustTimes[next] - 1e-9)
                {
                    lean[next] = s.X;
                    rate[next] = s.RateX;
                    next++;
                }
                double held = (k / PlantWash.SubSteps) * tick; // the tick's start
                PlantLaw.Step(m, ref s, 0, 0, new Double3(held < 1 - 1e-9 ? u1 : u0, 0, 0), dt);
            }
        }

        // A short gust bends a culm and a branch, and they return as the hand calculation's full law has it: the law's step at
        // 1 ms within 0.02 rad of the lean and 0.25 rad/s of the rate (its air damping linearised in the stem's own motion, the
        // hand's relative-velocity drag not), the culm in 5 m/s creeping back with one shallow dip, in 2 m/s and the branch
        // swinging back past rest. As the wash grid steps it (40 ms ticks, 20 ms sub-steps) within 0.05 rad.
        [Test]
        public void AGustBendsAndTheyReturnAsTheHandCalculation()
        {
            foreach (var g in Gusts)
            {
                PlantMechanics m = g.Make();
                double[] lean = new double[GustTimes.Length], rate = new double[GustTimes.Length];
                double[] coarse = new double[GustTimes.Length], coarseRate = new double[GustTimes.Length];
                Gust(m, g.U0, g.U1, 0.001, lean, rate);
                GustTicked(m, g.U0, g.U1, coarse, coarseRate);
                double worst = 0, worstRate = 0, worstCoarse = 0;
                for (int i = 0; i < GustTimes.Length; i++)
                {
                    Print($"  {g.Name}, t {GustTimes[i]:F2} s: {lean[i]:F5} rad ({rate[i]:F4} rad/s), at 40 ms {coarse[i]:F5}; hand {g.Lean[i]:F5} ({g.Rate[i]:F4})");
                    worst = Math.Max(worst, Math.Abs(lean[i] - g.Lean[i]));
                    worstRate = Math.Max(worstRate, Math.Abs(rate[i] - g.Rate[i]));
                    worstCoarse = Math.Max(worstCoarse, Math.Abs(coarse[i] - g.Lean[i]));
                }
                Print($"{g.Name}: the worst lean {worst:F4} rad, rate {worstRate:F3} rad/s; at 40 ms {worstCoarse:F4} rad");
                Assert.That(worst, Is.LessThan(0.001), g.Name + ": the lean");
                Assert.That(worstRate, Is.LessThan(0.01), g.Name + ": the rate");
                Assert.That(worstCoarse, Is.LessThan(0.05), g.Name + ": the lean at the grid's 40 ms");
            }
        }

        // The Gaussian expectation (statistical linearisation's means): for a bare stem Borgman's closed form, E[|v|v] = μ² + σ²
        // far from 0, (μ² + σ²)·erf(μ/√2σ) + μσ√(2/π)·e^(−μ²/2σ²) at μ = σ, E[|v|] = σ√(2/π) at μ = 0; for foliage the
        // Gauss–Hermite rule, against the closed form where the foliage's coefficient is constant (below its reconfiguration).
        [Test]
        public void TheExpectationIsGaussian()
        {
            PlantMechanics m = Green(0.6);
            double c = m.StemDrag;
            PlantAir.Expect(m, 5, 1, out double far, out _, out _);
            PlantAir.Expect(m, 1, 1, out double borgman, out _, out _);
            PlantAir.Expect(m, 0, 1, out double zero, out double along, out double across);
            const double borgmanHand = 1.8493202; // 2·erf(1/√2) + √(2/π)·e^(−½), erf(1/√2) = 0.682689492
            Print($"E[c|v|v]/c: {far / c:F7} at 5 ± 1 (26), {borgman / c:F7} at 1 ± 1 (Borgman {borgmanHand}); E[c|v|]/c at 0 ± 1 {across / c:F7} "
                + $"(√(2/π) {Math.Sqrt(2 / Math.PI):F7}), E[2c|v|]/c {along / c:F7}");
            Assert.That(far / c, Is.EqualTo(26).Within(1e-5));
            Assert.That(borgman / c, Is.EqualTo(borgmanHand).Within(1e-6));
            Assert.That(zero, Is.EqualTo(0).Within(1e-9 * c)); // erf(0) by A&S 7.1.26: within its 1.5e-7
            Assert.That(across / c, Is.EqualTo(Math.Sqrt(2 / Math.PI)).Within(1e-7));
            Assert.That(along / c, Is.EqualTo(2 * Math.Sqrt(2 / Math.PI)).Within(2e-7));
            PlantMechanics twig = Twig();
            double ct = twig.StemDrag + twig.FoliageDrag;
            PlantAir.Expect(twig, 2, 0.4, out double hermite, out _, out _);
            Print($"a twig at 2 ± 0.4 m/s: E[c|v|v] {hermite:E6} by Gauss–Hermite, {ct * 4.16:E6} closed");
            Assert.That(hermite, Is.EqualTo(ct * 4.16).Within(1e-5 * ct * 4.16));
        }

        // The plants read the field as the flight does: over the farmland in Windy (its hills, its belts' canopy strips and their
        // wakes) the plants' air at points over the ground matches WindField.At at the same step within 1e-9 m/s, a step's lee
        // above its lip left out (design.md §3.4).
        [Test]
        public void ThePlantsReadTheFlightsField()
        {
            MapWorld map = SlopeScenarios.Farmland();
            var world = new FlightWorld(map);
            using var wind = new WindField(WindSetup.ForMap(map, WindLevel.Windy), new FlightRandom(0x16A1), world.Hills);
            var plants = new PlantAir(wind);
            var rng = new Random(16);
            var xz = new XZ[1];
            var g = new GroundSample[1];
            int compared = 0, skipped = 0;
            double worst = 0;
            foreach (long step in new long[] { 0, 1234, 40000, 333333 })
            {
                var origin = new Double3(-1e4, 0, -1e4);
                wind.Begin(step, origin, 0);
                plants.Begin(step * FlightCore.Dt);
                for (int i = 0; i < 300; i++)
                {
                    double x = -480 + 960 * rng.NextDouble(), z = -480 + 960 * rng.NextDouble();
                    xz[0] = new XZ(x, z);
                    map.SampleGround(xz, g);
                    if (!(g[0].Height > double.NegativeInfinity)) continue;
                    var p = new Double3(x, g[0].Height + 0.2 + 12 * rng.NextDouble() * rng.NextDouble(), z);
                    if (world.Hills != null && world.Hills.Steps > 0 && world.Hills.Lee(x, z, wind.Mean, out LeeFrame lee) && p.Y >= lee.Lip)
                    {
                        skipped++;
                        continue;
                    }
                    Double3 a = wind.At(p, g[0].Height), b = plants.Air(p, g[0].Height);
                    worst = Math.Max(worst, (a - b).Length());
                    compared++;
                }
            }
            Print($"{compared} points over the farmland at 4 steps: the plants' air against the flight's within {worst:E2} m/s ({skipped} above a step's lip skipped)");
            Assert.That(compared, Is.GreaterThan(900));
            Assert.That(worst, Is.LessThan(1e-9));
        }

        // The closed form is the link's own response: a branch in a crown 5 m up and a green culm in the open, in Windy, driven step
        // by step (1 ms) by the field's air at their load points, against PlantAir at 20 ms over 60 s after 20 s: the same motion
        // (correlation), its size (rms) and its mean lean. The culm's turbulence is as strong as its mean, where the statistical
        // linearisation carries the mean; the branch's is a fifth of it.
        [Test]
        public void TheClosedFormIsTheLinksOwnResponse()
        {
            using var wind = new WindField(WindSetup.Of(WindLevel.Windy), new FlightRandom(0x16A2));
            var plants = new PlantAir(wind);
            foreach (var (name, m, baseHeight, maxCorr, maxRms, maxMean) in new[]
            {
                ("branch 3.5 m, base 5 m up", Branch(), 5.0, 0.95, 0.15, 0.10),
                ("green culm 0.97 m", Green(0.97), 0.0, 0.85, 0.25, 0.25),
            })
            {
                var basePoint = new Double3(3, baseHeight, -7);
                var load = new Double3(3, baseHeight + m.LoadPoint, -7);
                var s = new PlantState();
                double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0, mx = 0, my = 0;
                int n = 0;
                for (long k = 0; k < 80000; k++)
                {
                    wind.Begin(k, default, 0);
                    PlantLaw.Step(m, ref s, 0, 0, wind.At(load, 0), 0.001);
                    if (k < 20000 || k % 20 != 0) continue;
                    plants.Begin((k + 1) * 0.001);
                    plants.Wind(m, basePoint, 0, out PlantBend b);
                    // The fluctuation across the mean wind's way at this frame is the field's own (Mean's way).
                    double x = s.X, y = b.X;
                    sx += x; sy += y; sxx += x * x; syy += y * y; sxy += x * y;
                    mx += s.Z; my += b.Z;
                    n++;
                }
                double ax = sx / n, ay = sy / n, vx = sxx / n - ax * ax, vy = syy / n - ay * ay, corr = (sxy / n - ax * ay) / Math.Sqrt(vx * vy);
                double rms = Math.Sqrt(vy / vx);
                Print($"{name}: the bend's x by the step mean {ax:F5} rms {Math.Sqrt(vx):F5} rad, the closed form's mean {ay:F5} rms {Math.Sqrt(vy):F5}; "
                    + $"correlation {corr:F4}; z means {mx / n:F5} and {my / n:F5}");
                Assert.That(corr, Is.GreaterThan(maxCorr), name + ": the same motion");
                Assert.That(rms, Is.EqualTo(1).Within(maxRms), name + ": its size");
                Assert.That(Math.Abs(ay - ax), Is.LessThan(maxMean * Math.Max(Math.Abs(ax), Math.Sqrt(vx))), name + ": its mean");
            }
        }

        // One law, one answer: two flights' fields from one seed give the plants the same bends bit for bit, and a frame's sample
        // allocates nothing.
        [Test]
        public void ThePlantsAreDeterministicAndAllocateNothing()
        {
            using var a = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(77));
            using var b = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(77));
            var pa = new PlantAir(a);
            var pb = new PlantAir(b);
            PlantMechanics culm = Green(0.6), branch = Branch();
            for (int i = 0; i < 50; i++)
            {
                double t = 3.7 * i;
                pa.Begin(t);
                pb.Begin(t);
                var p = new Double3(11 * i - 200, 0, 7 * i);
                pa.Wind(culm, p, 0, out PlantBend x);
                pb.Wind(culm, p, 0, out PlantBend y);
                Assert.That(x.X == y.X && x.Z == y.Z && x.RateX == y.RateX && x.RateZ == y.RateZ, $"frame {i}");
                pa.Wind(branch, p + new Double3(0, 5, 0), 0, out x);
                pb.Wind(branch, p + new Double3(0, 5, 0), 0, out y);
                Assert.That(x.X == y.X && x.Z == y.Z && x.RateX == y.RateX && x.RateZ == y.RateZ, $"frame {i}, the branch");
            }
            Frames(pa, culm, branch, 2);
            long before = AllocProbe.Before();
            Frames(pa, culm, branch, 200);
            long bytes = AllocProbe.After(before);
            Print($"{bytes} B over 200 frames of two plants");
            Assert.That(bytes, Is.Zero, AllocProbe.Report());
        }

        static void Frames(PlantAir air, in PlantMechanics culm, in PlantMechanics branch, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                air.Begin(0.02 * i);
                air.Wind(culm, new Double3(i, 0, 0), 0, out _);
                air.Wind(branch, new Double3(i, 5, 0), 0, out _);
            }
        }
    }
}
