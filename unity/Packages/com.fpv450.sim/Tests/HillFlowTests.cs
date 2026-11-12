using System;
using System.Globalization;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // w7-farmland-wind 1.1: the wind over low hills and in a step's lee (HillFlow, WindField.At(point, ground)), against the
    // published references, each with its known-wrong model.
    // - A 2-D ridge and a 3-D axisymmetric hill: Taylor and Lee (1984)'s ΔS_max = 2.0 and 1.6 h/L1, ±15 % (Kaimal and
    //   Finnigan 1994); the outer layer's potential flow alone (Jackson and Hunt's inner layer left out) is the known-wrong.
    // - A laid node on a hillside: the wind at its own height over its own ground; the drone's ground is the known-wrong.
    // - A step's lee: the bubble's reverse flow to X_R = 7.2 D, gone past the recovery; the unseparated field the known-wrong.
    // - The grids are the same bits from the same ground, and a sample allocates nothing.
    public sealed class HillFlowTests
    {
        static string F(double x, string format = "0.0000") => x.ToString(format, CultureInfo.InvariantCulture);

        static readonly WindPreset Profile = WindPreset.Of(WindLevel.Severe);

        /// <summary>The ridge's and the hill's height and half-length L1 (m; assumed: the farmland's rolling scale, h/L1 0.05).</summary>
        const double H = 10, L1 = 200;

        // A ground of height(x, z) everywhere.
        static GroundSampler Ground(Func<double, double, double> height) => (points, results) =>
        {
            for (int i = 0; i < points.Length; i++)
                results[i] = new GroundSample { Height = height(points[i].X, points[i].Z), Normal = new Double3(0, 1, 0), Flags = GroundFlags.None };
        };

        static HillFlow Hills(Func<double, double, double> height, TerrainStep[] steps = null) =>
            HillFlow.Build(Ground(height), 0, 0, 0, steps, Profile.Roughness, Profile.Displacement, Profile.ProfileTop);

        // λ = ln((ζ)/z0) at a height over the ground (the field's own log).
        static double Lambda(double h) => Math.Log((Math.Min(h, Profile.ProfileTop) - Profile.Displacement) / Profile.Roughness);

        // The along-x fractional speed-up at (x, 0) and h over the ground (the wind along +x).
        static double AlongX(HillFlow hills, double x, double h)
        {
            Assert.That(hills.Sample(x, 0, Lambda(h), out HillSample s), Is.True);
            return s.S11;
        }

        // The witch of Agnesi's ridge across x, and the axisymmetric hill whose transform is the witch's, e^(−|k|a):
        // h = H/(1 + r²/a²)^(3/2), L1 = a·√(2^(2/3) − 1).
        static double Ridge(double x, double z) => H / (1 + x * x / (L1 * L1));
        static readonly double HillA = L1 / Math.Sqrt(Math.Pow(2, 2.0 / 3) - 1);
        static double Hill(double x, double z)
        {
            double q = 1 + (x * x + z * z) / (HillA * HillA);
            return H / (q * Math.Sqrt(q));
        }

        [Test]
        public void ARidgeAndAHill_SpeedUpAsTheGuidelines()
        {
            HillFlow ridge = Hills(Ridge), hill = Hills(Hill);
            double ratio = H / L1;
            foreach (var (name, flow, b, a) in new[] { ("2-D ridge", ridge, 2.0, 3.0), ("3-D hill", hill, 1.6, 4.0) })
            {
                double peak = 0, peakAt = 0;
                string line = "";
                foreach (double h in new[] { 0.5, 1, 2, 5, 10, 20, 40, 80 })
                {
                    double s = AlongX(flow, 0, h);
                    if (s > peak) { peak = s; peakAt = h; }
                    line += $" {F(h, "0.#")} m {F(s)} (guideline {F(b * ratio * Math.Exp(-a * h / L1))});";
                }
                double lee = AlongX(flow, Math.Sqrt(3) * L1, 5), upwind = AlongX(flow, -Math.Sqrt(3) * L1, 5), crest5 = AlongX(flow, 0, 5);
                TestContext.Out.WriteLine($"{name}, h/L1 {F(ratio, "0.00")}: ΔS over the crest{line} peak {F(peak)} at {F(peakAt, "0.#")} m = "
                    + $"{F(peak / ratio, "0.00")} h/L1 against the published {F(b, "0.0")} ± 15 %; at 5 m √3·L1 downwind {F(lee)} and upwind {F(upwind)} "
                    + $"({F(lee / crest5, "0.000")} and {F(upwind / crest5, "0.000")} of the crest's; the witch's potential flow −0.125)");
                Assert.That(peak / ratio, Is.InRange(b * 0.85, b * 1.15), name + ": the published speed-up");
                Assert.That(lee, Is.LessThan(0), name + ": the lee foot is slowed");
            }

            // The known-wrong: the outer layer's potential flow alone, the inner layer's speed-up left out (ΔS = h/L1 at the
            // surface over the witch's ridge, derived). It misses the guideline's band.
            double potential = ratio;
            Assert.That(potential / ratio, Is.Not.InRange(2.0 * 0.85, 2.0 * 1.15));
            TestContext.Out.WriteLine($"known-wrong: the potential flow alone {F(potential / ratio, "0.00")} h/L1 over the ridge");
        }

        [Test]
        public void TheSameGround_TheSameBits_AndASampleAllocatesNothing()
        {
            HillFlow a = Hills(Hill), b = Hills(Hill);
            var rng = new Random(5);
            ulong ha = 14695981039346656037, hb = ha;
            for (int k = 0; k < 2000; k++)
            {
                double x = rng.NextDouble() * 4000 - 2000, z = rng.NextDouble() * 4000 - 2000, l = rng.NextDouble() * 7.5 - 0.3;
                a.Sample(x, z, l, out HillSample sa);
                b.Sample(x, z, l, out HillSample sb);
                foreach (double v in new[] { sa.S11, sa.S12, sa.S22, sa.G1, sa.G2 }) ha = (ha ^ (ulong)BitConverter.DoubleToInt64Bits(v)) * 1099511628211;
                foreach (double v in new[] { sb.S11, sb.S12, sb.S22, sb.G1, sb.G2 }) hb = (hb ^ (ulong)BitConverter.DoubleToInt64Bits(v)) * 1099511628211;
            }
            Assert.That(hb, Is.EqualTo(ha));
            double Run()
            {
                double sum = 0;
                for (int k = 0; k < 10000; k++)
                {
                    a.Sample(k * 0.3 - 1500, 700 - k * 0.1, 0.0007 * k, out HillSample s);
                    sum += s.S11;
                }
                return sum;
            }
            Run(); // the tiered JIT's first pass
            long before = GC.GetAllocatedBytesForCurrentThread();
            double sum = Run();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (allocated != 0) Assert.Fail(allocated + " B allocated"); // no constraint built before the read (w3-farmland-ground 1.2's 24 B)
            TestContext.Out.WriteLine($"2000 samples the same bits ({ha:x16}); 10000 samples 0 B ({F(sum, "0.0")})");
        }

        // A field of a pilot's level over the hills, its turbulence off so its mean shows (a test's preset).
        static WindField MeanField(HillFlow hills, ulong seed = 3)
        {
            WindPreset p = Profile;
            p.Intensity = 0;
            return new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed), p, hills);
        }

        // The direction the field's mean blows toward at step 0 (its drawn bearing with the meander there).
        static Double3 Blowing()
        {
            using (WindField probe = MeanField(null))
            {
                probe.Begin(0, default, 0);
                Double3 m = probe.Mean;
                return m * (1 / m.Length());
            }
        }

        [Test]
        public void ALaidNodeOnAHillside_FeelsItsOwnGround()
        {
            // The ridge across the drawn wind, the drone at its upwind foot and a node lying on its crest 0.1 mm over the ground.
            Double3 d = Blowing();
            Func<double, double, double> across = (x, z) => Ridge(x * d.X + z * d.Z, 0);
            HillFlow hills = Hills(across);
            using (WindField wind = MeanField(hills))
            {
                Double3 drone = d * (-3 * L1);
                double droneGround = across(drone.X, drone.Z);
                wind.Begin(0, drone + new Double3(0, droneGround + 5, 0), droneGround);
                double crest = across(0, 0);
                var node = new Double3(0, crest + 1e-4, 0);
                Double3 own = wind.At(node, crest), wrong = wind.At(node);
                var tenUp = new Double3(0, crest + 10, 0);
                Double3 overCrest = wind.At(tenUp, crest);
                Double3 plane;
                using (WindField flat = MeanField(null))
                {
                    flat.Begin(0, default, 0);
                    plane = flat.At(new Double3(0, 10, 0), 0);
                }
                TestContext.Out.WriteLine($"a node on the crest, {F(crest, "0.00")} m over the drone's ground {F(droneGround, "0.00")} m: its own ground "
                    + $"{F(own.Length())} m/s; the drone's ground (known-wrong) {F(wrong.Length(), "0.00")} m/s; 10 m over the crest {F(overCrest.Length(), "0.00")} m/s "
                    + $"against the plane's {F(plane.Length(), "0.00")} (ΔS {F(overCrest.Length() / plane.Length() - 1)})");
                Assert.That(own.Length(), Is.EqualTo(0), "a node in the cover's still layer");
                Assert.That(wrong.Length(), Is.GreaterThan(1), "the drone's ground puts it metres up");
                Assert.That(overCrest.Length(), Is.GreaterThan(plane.Length() * 1.05), "the crest's speed-up");
            }
        }

        // "Bit-exact replays": a Severe flight with its line over the farmland's highest hill, its wind over the hills and each node
        // over its own ground, recorded and replayed bit for bit (the replay rebuilds the hills from the world it opens).
        [Test]
        public void AFlightOverTheFarmlandsHills_Replays()
        {
            MapWorld map = MapWorld.Load(RangeScenarios.Root, "farmland");
            DroneSpec spec = DroneSpec.Parse(System.IO.File.ReadAllText(ContactScenarios.RepositoryFile("assets-src/drone/drone.json")));
            const double x0 = 40, z = 924; // the crest at (86, 924), 46 m east
            var layout = new Double3[40];
            for (int i = 0; i < layout.Length; i++) layout[i] = new Double3(x0 - layout.Length + i, SlopeScenarios.Ground(map, x0 - layout.Length + i, z).Height, z);
            FlightSetup setup = TetherScenarios.Setup(spec, WindLevel.Severe, layout: layout, cuts: true);
            double stand = TetherScenarios.Stand(setup);
            setup.World = new FlightWorld(map);
            setup.StartPosition = new Double3(x0, SlopeScenarios.Ground(map, x0, z).Height + stand, z);
            var core = new FlightCore(setup);
            Assert.That(core.Wind.Hills, Is.Not.Null, "a pilot's level over a terrain carries its hills");
            Log.LogHeader h = Log.LogHeader.For(setup, core);
            h.Build = "simtests";
            h.Isa = "test";
            h.WorldId = "farmland";
            h.SetDrone(spec, spec.Nominal);
            var log = new System.IO.MemoryStream();
            var recorder = new Log.FlightRecorder(log, h, Log.FlightRecorder.DefaultCapacity, ownsStream: false);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            Double3 s0 = setup.StartPosition;
            long allocated = 0;
            const int steps = 6000;
            for (int k = 0; k < steps; k++)
            {
                StickSample stick;
                double t = k * FlightCore.Dt;
                if (k < 20) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else
                {
                    pilot.HoldVelocity = false;
                    double x = t < 2 ? s0.X : s0.X + 4 * (t - 2);
                    pilot.Target = new Double3(x, SlopeScenarios.Ground(map, x, z).Height + stand + Math.Min(5, 2 * t), z);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                }
                long before = GC.GetAllocatedBytesForCurrentThread();
                core.Advance(stick);
                if (k >= 1) allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
            }
            recorder.Close();
            Double3 end = core.State.Position;
            string line = $"{core.Tether.PaidOut:F1} m paid out, broken {core.Tether.Broken}";
            core.Wind.Dispose();
            ContactScenarios.RepositoryMaterials(out ulong materials);
            Log.ReplayResult same = Log.FlightReplay.Run(new System.IO.MemoryStream(log.ToArray()), id => id == "farmland" ? new FlightWorld(map) : null,
                spec.ContentHash, tetherHash: TetherScenarios.Table().ContentHash, materialsHash: materials);
            TestContext.Out.WriteLine($"{steps} steps over the hill to ({F(end.X, "0.0")}, {F(end.Y, "0.0")}, {F(end.Z, "0.0")}), {line}; {allocated} B after the first step; "
                + same.Message);
            Assert.That(same.Outcome, Is.EqualTo(Log.ReplayOutcome.Match), same.Message);
            Assert.That(same.Steps, Is.EqualTo(steps));
            if (allocated != 0) Assert.Fail(allocated + " B allocated after the first step");
        }

        [Test]
        public void TheFlowFollowsTheGround_UpTheWindwardSlope()
        {
            Double3 d = Blowing();
            Func<double, double, double> across = (x, z) => Ridge(x * d.X + z * d.Z, 0);
            using (WindField wind = MeanField(Hills(across)))
            {
                wind.Begin(0, default, 0);
                foreach (double s in new[] { -L1, L1 })
                {
                    Double3 p = d * s;
                    double g = across(p.X, p.Z), slope = -2 * H * s / (L1 * L1) / Math.Pow(1 + s * s / (L1 * L1), 2);
                    Double3 w = wind.At(p + new Double3(0, g + 5, 0), g);
                    double horizontal = Math.Sqrt(w.X * w.X + w.Z * w.Z);
                    TestContext.Out.WriteLine($"at {F(s, "0")} m along the wind, 5 m up: w {F(w.Y)} m/s over {F(horizontal, "0.00")} m/s, "
                        + $"the streamline's slope {F(w.Y / horizontal)} against the ground's {F(slope)}");
                    Assert.That(Math.Sign(w.Y), Is.EqualTo(Math.Sign(slope)), "up the windward slope, down the lee");
                    Assert.That(w.Y / horizontal, Is.EqualTo(slope).Within(0.3 * Math.Abs(slope)));
                }
            }
        }

        [Test]
        public void AStepsLee_SeparatesAndReattaches()
        {
            // A 1.5 m step along z at x = 0, its high side the one the drawn wind comes from, so its lee lies downwind; then the
            // same step facing the wind, which has no lee.
            const double D = 1.5;
            var steps = new[] { new TerrainStep(true, 0, -1000, 1000, D, D, "test") };
            Double3 d = Blowing();
            double from = d.X < 0 ? 1 : -1; // the side (sign of x) the wind comes from
            Func<double, double, double> lee = (x, z) => x * from >= 0 ? D : 0, facing = (x, z) => x * from <= 0 ? D : 0;
            HillFlow hills = Hills(lee, steps);
            Assert.That(hills.Steps, Is.EqualTo(1));
            double xr = Cavity.StepReattachment * D;
            using (WindField wind = MeanField(hills))
            using (WindField unseparated = MeanField(Hills(lee)))
            {
                wind.Begin(0, new Double3(20 * from, 30, 0), D);
                unseparated.Begin(0, new Double3(20 * from, 30, 0), D);
                string line = "";
                double least = 0;
                foreach (double xi in new[] { 0.25, 0.5, 0.75, 1.0, 1.5, 2.0, 2.5 })
                {
                    var p = new Double3(-from * xi * xr, 0.5, 0); // 0.5 m over the floor, 1.0 m below the lip
                    Double3 a = wind.At(p, 0), b = unseparated.At(p, 0);
                    double ua = -from * a.X, ub = -from * b.X; // across the step, downwind
                    if (xi < 1) least = Math.Min(least, ua);
                    line += $" ξ/X_R {F(xi, "0.00")}: {F(ua, "0.00")} m/s, w {F(a.Y, "0.00")} (unseparated {F(ub, "0.00")});";
                }
                TestContext.Out.WriteLine($"the wind {F(Math.Abs(d.X), "0.00")} across the step, X_R {F(xr, "0.0")} m; 0.5 m over the lee's floor:{line}");
                Assert.That(least, Is.LessThan(0), "reverse flow in the bubble");
                Assert.That(Same(wind, unseparated, new Double3(-from * 2.5 * xr, 0.5, 0)), Is.True, "past the recovery, the open field");
                Assert.That(Same(wind, unseparated, new Double3(-from * 0.5 * xr, 0.5, 0)), Is.False, "the known-wrong: no bubble");
            }
            var faced = new[] { new TerrainStep(true, 0, -1000, 1000, D, D, "test") };
            using (WindField wind = MeanField(Hills(facing, faced)))
            using (WindField unseparated = MeanField(Hills(facing)))
            {
                wind.Begin(0, default, 0);
                unseparated.Begin(0, default, 0);
                Assert.That(Same(wind, unseparated, new Double3(from * 0.5 * xr, 0.5, 0)), Is.True, "a face to the wind: not modelled");
            }
        }

        // The two fields' wind at p over the ground 0, bit for bit.
        static bool Same(WindField a, WindField b, Double3 p)
        {
            Double3 x = a.At(p, 0), y = b.At(p, 0);
            return x.X == y.X && x.Y == y.Y && x.Z == y.Z;
        }
    }
}
