using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The lens's water (w21-rain-on-camera 1.1, FEED-13, D-126): the spec's scenarios. The arrivals against the law written again
    /// here with System.Math; the drops held, slid and shed by the forces in the lens's frame; the flight unchanged with the lens
    /// stepping beside it; determinism, bounds, the water's books and 0 B.
    /// </summary>
    public class LensWaterTests
    {
        const double Deg = Math.PI / 180;
        const int Field = FlightCore.FieldSteps;

        static DroneSpec Drone => RepositoryDrone.Spec();
        static MassModel Airframe => Drone.BuildMass(Drone.Nominal);

        static LensWater Lens(double rate, LensSpec spec = null, ulong seed = 0x450F) => new LensWater(Drone, Airframe, rate, seed, spec);

        // A body turned by angle about one of its axes (0 x, 1 y, 2 z): R·(0, 0, 1) = (0, −sin a, cos a) about x, nose down.
        static Quat Turn(int axis, double angle)
        {
            double s = Math.Sin(angle / 2), c = Math.Cos(angle / 2);
            return axis == 0 ? new Quat(s, 0, 0, c) : axis == 1 ? new Quat(0, s, 0, c) : new Quat(0, 0, s, c);
        }

        static PoseFrame Pose(long step, Quat attitude, Double3 velocity, Double3 airAtCamera)
        {
            var p = new PoseFrame
            {
                Step = step,
                Position = new Double3(0, 10, velocity.Z * step * FlightCore.Dt),
                Attitude = attitude,
                Velocity = velocity,
                AirVelocityAtCamera = airAtCamera,
                Armed = true,
            };
            double hover = Math.Sqrt(4.8 * 9.80665 / 4 / Drone.Props.ThrustCoefficient);
            p.RotorSpeed = new Corners { RearRight = hover, FrontRight = hover, RearLeft = hover, FrontLeft = hover };
            return p;
        }

        // A steady flight's field k: the same attitude and velocity, the air relative to the lens in the camera's axes.
        static void Steady(LensWater lens, long k, Quat attitude, Double3 velocity, Double3 air)
        {
            long s = k * Field;
            lens.Advance(Pose(s, attitude, velocity, air), Pose(s + Field / 2, attitude, velocity, air), Pose(s + Field, attitude, velocity, air));
        }

        // The arrivals by the law (design §3.1), written again: Marshall–Palmer's slope, the intercept carrying the rate, 12 bins at
        // their geometric mids' Atlas fall, onto the dome's 19 facets; no shelter.
        static double Expected(double rate, double curvature, Quat attitude, Double3 air, out double water)
        {
            double[] edges = { 0.1, 0.2, 0.3, 0.5, 0.7, 1.0, 1.3, 1.7, 2.2, 2.8, 3.5, 4.5, 6.0 };
            double l = 4.1 * Math.Pow(rate, -0.21), carried = 0;
            var n = new double[12];
            var v = new double[12];
            var vol = new double[12];
            Func<double, double> cube = d => -Math.Exp(-l * d) * (d * d * d / l + 3 * d * d / (l * l) + 6 * d / (l * l * l) + 6 / (l * l * l * l));
            for (int b = 0; b < 12; b++)
            {
                double d0 = edges[b], d1 = edges[b + 1];
                n[b] = (Math.Exp(-l * d0) - Math.Exp(-l * d1)) / l;
                vol[b] = Math.PI / 6 * (cube(d1) - cube(d0)) / n[b] * 1e-9;
                v[b] = 9.65 - 10.3 * Math.Exp(-0.6 * Math.Sqrt(d0 * d1));
                carried += n[b] * v[b] * vol[b];
            }
            double n0 = rate / 3.6e6 / carried;
            Double3 down = attitude.InverseRotate(new Double3(0, -1, 0));
            double count = 0, clear = 0.0045;
            water = 0;
            int[] rings = { 1, 6, 12 };
            for (int ring = 0; ring < 3; ring++)
                for (int s = 0; s < rings[ring]; s++)
                {
                    double r0 = clear * ring / 3, r1 = clear * (ring + 1) / 3, p0 = 2 * Math.PI * s / rings[ring], p1 = 2 * Math.PI * (s + 1) / rings[ring];
                    double rc = ring > 0 ? Math.Sqrt(0.5 * (r0 * r0 + r1 * r1)) : 0, pc = 0.5 * (p0 + p1);
                    double x = rc * Math.Cos(pc), y = rc * Math.Sin(pc);
                    double nz = double.IsPositiveInfinity(curvature) ? 1 : Math.Sqrt(curvature * curvature - rc * rc) / curvature;
                    double nx = double.IsPositiveInfinity(curvature) ? 0 : x / curvature, ny = double.IsPositiveInfinity(curvature) ? 0 : y / curvature;
                    double area = 0.5 * (r1 * r1 - r0 * r0) * (p1 - p0) / nz;
                    for (int b = 0; b < 12; b++)
                    {
                        double ux = air.X + down.X * v[b], uy = air.Y + down.Y * v[b], uz = air.Z + down.Z * v[b];
                        double onto = -(ux * nx + uy * ny + uz * nz);
                        if (onto <= 0) continue;
                        count += n0 * n[b] * onto * area;
                        water += n0 * n[b] * onto * area * vol[b];
                    }
                }
            return count;
        }

        static void Print(string line) => TestContext.Progress.WriteLine(line);

        [Test]
        public void ForwardFlightWetsTheLensMoreThanAHoverByTheComputedRatio()
        {
            // A flat lens, level: a hover in a 5 m/s headwind against 20 m/s of forward flight. The fall runs along the glass, so
            // the ratio is the normal components' exactly.
            var flat = new LensSpec { CurvatureRadius = double.PositiveInfinity };
            LensWater hover = Lens(5, flat), forward = Lens(5, flat);
            Steady(hover, 0, Quat.Identity, default, new Double3(0, 0, -5));
            Steady(forward, 0, Quat.Identity, new Double3(0, 0, 20), new Double3(0, 0, -20));
            Print($"flat, level: hover {hover.ArrivalRate:F4} /s, forward {forward.ArrivalRate:F4} /s, ratio {forward.ArrivalRate / hover.ArrivalRate:F6}");
            Assert.That(forward.ArrivalRate / hover.ArrivalRate, Is.EqualTo(4).Within(1e-12));
            Assert.That(forward.WaterRate / hover.WaterRate, Is.EqualTo(4).Within(1e-12));
            Assert.That(hover.ArrivalRate, Is.EqualTo(Expected(5, double.PositiveInfinity, Quat.Identity, new Double3(0, 0, -5), out _)).Within(1e-9).Percent);

            // The dome at a cruise's 25° nose-down: the same lens in a 5 m/s headwind hover and at 20 m/s, against the law.
            Quat pitched = Turn(0, 25 * Deg);
            Double3 slow = pitched.InverseRotate(new Double3(0, 0, -5)), fast = pitched.InverseRotate(new Double3(0, 0, -20));
            LensWater h = Lens(5), f = Lens(5);
            Steady(h, 0, pitched, default, slow);
            Steady(f, 0, pitched, new Double3(0, 0, 20), fast);
            double eh = Expected(5, 0.008, pitched, slow, out double wh), ef = Expected(5, 0.008, pitched, fast, out double wf);
            Print($"dome, 25° down: hover {h.ArrivalRate:F4} /s ({h.WaterRate * 1e9:F4} mm³/s), forward {f.ArrivalRate:F4} /s "
                + $"({f.WaterRate * 1e9:F4} mm³/s); the law {eh:F4} and {ef:F4}; sheltered {h.Sheltered:F3}, {f.Sheltered:F3}");
            Assert.That(h.Sheltered, Is.Zero, "nothing shades drops arriving from ahead");
            Assert.That(f.Sheltered, Is.Zero);
            Assert.That(h.ArrivalRate, Is.EqualTo(eh).Within(1e-9).Percent);
            Assert.That(f.ArrivalRate, Is.EqualTo(ef).Within(1e-9).Percent);
            Assert.That(f.WaterRate, Is.EqualTo(wf).Within(1e-9).Percent);
            Assert.That(f.ArrivalRate, Is.GreaterThan(3 * h.ArrivalRate));
        }

        [Test]
        public void FacingIntoTheWindWetsTheLensMoreThanFacingAway()
        {
            LensWater into = Lens(5), away = Lens(5), calm = Lens(5);
            Steady(into, 0, Quat.Identity, default, new Double3(0, 0, -2));
            Steady(away, 0, Quat.Identity, default, new Double3(0, 0, 2));
            Steady(calm, 0, Quat.Identity, default, default);
            double open = Expected(5, 0.008, Quat.Identity, new Double3(0, 0, 2), out _);
            Print($"level hover, 2 m/s: into {into.ArrivalRate:F4} /s, away {away.ArrivalRate:F4} /s (open {open:F4}, sheltered "
                + $"{away.Sheltered:F3}), calm {calm.ArrivalRate:F4} /s");
            Assert.That(into.ArrivalRate, Is.GreaterThan(10 * away.ArrivalRate));
            // From behind, only the dome's upper rim faces the steep large drops, and the payload's front end shades much of it.
            Assert.That(away.ArrivalRate, Is.LessThan(calm.ArrivalRate));
            Assert.That(away.Sheltered, Is.GreaterThan(0.2));
            Assert.That(away.ArrivalRate, Is.EqualTo(open * (1 - away.Sheltered)).Within(1e-9).Percent);
            Assert.That(calm.Sheltered, Is.Zero, "straight up from the lens is open sky");
        }

        [Test]
        public void SunnyKeepsTheLensDry()
        {
            LensWater lens = Lens(0);
            Quat pitched = Turn(0, 25 * Deg);
            for (int k = 0; k < 500; k++) Steady(lens, k, pitched, new Double3(0, 0, 20), pitched.InverseRotate(new Double3(0, 0, -20)));
            Assert.That(lens.ArrivalRate, Is.Zero);
            Assert.That(lens.Arrived, Is.Zero);
            Assert.That(lens.Drops.Length, Is.Zero);
        }

        [Test]
        public void AHeldBankSlidesLargeDropsTowardTheLowSideAndSmallOnesStay()
        {
            // A 30° bank held at a steady drift with the air, so the lens feels gravity at the tilt and no air.
            Quat bank = Turn(2, 30 * Deg);
            Double3 g = bank.InverseRotate(FlightCore.Gravity), drift = bank.Rotate(new Double3(3, 0, 0));
            LensWater large = Lens(0), small = Lens(0), level = Lens(0);
            large.Place(0, 0, 20e-9);
            small.Place(0, 0, 1e-9);
            level.Place(0, 0, 20e-9);
            for (int k = 0; k < 2; k++) // 40 ms: the large drop is under way, not yet at the glass's edge
            {
                Steady(large, k, bank, drift, default);
                Steady(small, k, bank, drift, default);
                Steady(level, k, Quat.Identity, default, default);
            }
            LensDrop a = large.Drops[0], b = small.Drops[0], c = level.Drops[0];
            Print($"gravity on the glass ({g.X:F2}, {g.Y:F2}); 20 mm³: ({a.X * 1e3:F2}, {a.Y * 1e3:F2}) mm at ({a.VelocityX * 100:F1}, "
                + $"{a.VelocityY * 100:F1}) cm/s, elongation {a.Elongation:F2}, {large.TrailOf(0).Length} trail points; level ({c.X * 1e3:F2}, "
                + $"{c.Y * 1e3:F2}) mm; 1 mm³ at ({b.X * 1e3:F2}, {b.Y * 1e3:F2})");
            Assert.That(a.Sliding, Is.True);
            Assert.That(Math.Sign(a.X), Is.EqualTo(Math.Sign(g.X)), "toward the low side");
            Assert.That(a.VelocityX / a.VelocityY, Is.EqualTo(g.X / g.Y).Within(1e-6), "along gravity at the tilt");
            Assert.That(c.X, Is.Zero, "level, straight down");
            Assert.That(b.Sliding, Is.False);
            Assert.That(b.X, Is.Zero);
            Assert.That(b.Y, Is.Zero);
        }

        [Test]
        public void FastFlightKeepsSmallDropsAndTheAirDrivesLargeOnesAcross()
        {
            Quat pitched = Turn(0, 25 * Deg);
            Double3 v = new Double3(0, 0, 20), air = pitched.InverseRotate(new Double3(0, 0, -20));
            LensWater small = Lens(0), large = Lens(0), still = Lens(0);
            small.Place(0, 0, 0.1e-9);
            large.Place(0, 0, 10e-9);
            still.Place(0, 0, 10e-9);
            Steady(small, 0, pitched, v, air);
            Steady(large, 0, pitched, v, air);
            Steady(still, 0, pitched, v, default);
            LensDrop s = small.Drops[0], l = large.Drops[0], t = still.Drops[0];
            Print($"20 m/s, 25° down (air {air.X:F1}, {air.Y:F2}, {air.Z:F2}): 0.1 mm³ sliding {s.Sliding}; 10 mm³ at "
                + $"({l.VelocityX * 100:F1}, {l.VelocityY * 100:F1}) cm/s; 10 mm³ with no air sliding {t.Sliding}");
            Assert.That(s.Sliding, Is.False);
            Assert.That(l.Sliding, Is.True);
            Assert.That(l.VelocityY, Is.LessThan(0), "down the glass, with the edge-on air and gravity");
            Assert.That(t.Sliding, Is.False, "gravity alone holds it: the air drives it");

            // Flown on, the large drop crosses the glass and is shed; the small one stays.
            LensWater both = Lens(0);
            both.Place(0.003, 0.0025, 0.1e-9);
            both.Place(0, -0.0005, 10e-9);
            for (int k = 0; k < 50; k++) Steady(both, k, pitched, v, air);
            Print($"after 1 s: {both.Drops.Length} drop, shed {both.Shed}, trail water {both.TrailWater * 1e9:F3} mm³");
            Assert.That(both.Shed, Is.EqualTo(1));
            Assert.That(both.Drops.Length, Is.EqualTo(1));
            Assert.That(both.Drops[0].Volume, Is.EqualTo(0.1e-9));
        }

        [Test]
        public void TheFlightIsTheSameWithTheLensOnAndTheLensIsDeterministic()
        {
            FlightSetup setup = GoldenFlights.PoweredSetup(Drone);
            setup.Seed = 0x450F;
            setup.Wind = WindSetup.Of(WindLevel.Windy);
            setup.Rain = RainLevel.SevereRain;
            setup.CameraPosition = Drone.CameraPosition;
            FlightSetup bare = GoldenFlights.PoweredSetup(Drone);
            bare.Seed = 0x450F;
            bare.Wind = WindSetup.Of(WindLevel.Windy);
            bare.Rain = RainLevel.SevereRain;
            bare.CameraPosition = Drone.CameraPosition;
            var withLens = new FlightCore(setup);
            var without = new FlightCore(bare);
            var poses = new PoseBuffer();
            withLens.GetPose(out PoseFrame first);
            withLens.GetSignals(out FeedSignals signals);
            poses.Begin(0, first, signals);
            double rate = signals.RainRateMmPerHour;
            LensWater a = new LensWater(Drone, Airframe, rate, setup.Seed), b = new LensWater(Drone, Airframe, rate, setup.Seed);
            var history = new PoseFrame[1001]; // every half field's pose
            history[0] = first;
            for (int k = 0; k < 10000; k++)
            {
                StickSample stick = GoldenFlights.PoweredStick(k);
                withLens.Advance(stick);
                without.Advance(stick);
                withLens.GetPose(out PoseFrame pose);
                withLens.GetSignals(out signals);
                poses.Publish(pose, signals);
                if ((k + 1) % (Field / 2) == 0) history[(k + 1) / (Field / 2)] = pose;
                Assert.That(pose.AirVelocityAtCamera.X == signals.AirVelocityAtCamera.X && pose.AirVelocityAtCamera.Z == signals.AirVelocityAtCamera.Z);
                if (k % 17 == 0) a.Advance(poses); // a render's frames, out of step with the fields
            }
            a.Advance(poses);
            // The same fields from the whole history, field by field.
            for (long f = 0; f + 1 <= 10000 / Field; f++)
                b.Advance(history[2 * f], history[2 * f + 1], history[2 * f + 2]);
            FlightState s = withLens.State, t = without.State;
            Assert.That(s.Position.X == t.Position.X && s.Position.Y == t.Position.Y && s.Position.Z == t.Position.Z
                && s.Velocity.X == t.Velocity.X && s.Attitude.W == t.Attitude.W, "the flight never reads the lens");
            Print($"Severe rain {rate:F1} mm/h, Windy, the powered golden's 10 s: {a.Arrived} arrived, {a.Merged} merged, {a.Shed} shed, "
                + $"{a.Drops.Length} on the glass ({a.Water * 1e9:F3} mm³); field {a.Field}");
            Assert.That(a.Field, Is.EqualTo(b.Field));
            Assert.That(a.Arrived, Is.GreaterThan(0));
            Assert.That(a.Drops.Length, Is.EqualTo(b.Drops.Length));
            for (int i = 0; i < a.Drops.Length; i++)
            {
                LensDrop p = a.Drops[i], q = b.Drops[i];
                Assert.That(p.Id == q.Id && p.X == q.X && p.Y == q.Y && p.Volume == q.Volume && p.Elongation == q.Elongation
                    && p.TrailCount == q.TrailCount, $"drop {i}");
            }
        }

        [Test]
        public void TheCountStaysBoundedTheWaterBalancesAndAFieldAllocatesNothing()
        {
            // Severe rain's top, 50 mm/h, in a headwind hover (no air sweeps the glass) and then a fast cruise in it.
            LensWater lens = Lens(50);
            Quat pitched = Turn(0, 25 * Deg);
            int most = 0, longest = 0;
            for (int k = 0; k < 6000; k++)
            {
                if (k < 3000) Steady(lens, k, Quat.Identity, default, new Double3(0, 0, -6));
                else Steady(lens, k, pitched, new Double3(0, 0, 20), pitched.InverseRotate(new Double3(0, 0, -26)));
                most = Math.Max(most, lens.Drops.Length);
                for (int i = 0; i < lens.Drops.Length; i++) longest = Math.Max(longest, lens.Drops[i].TrailCount);
                if (k == 2999) Print($"after 60 s of hover: {lens.Drops.Length} drops, {lens.Water * 1e9:F2} mm³ on the glass");
            }
            double books = lens.Water + lens.ShedWater + lens.TrailWater;
            Print($"then 60 s of cruise: {lens.Arrived} arrived, {lens.Merged} merged, {lens.Shed} shed; most drops {most}, longest trail "
                + $"{longest}; water {lens.ArrivedWater * 1e9:F2} mm³ = {lens.Water * 1e9:F2} on + {lens.ShedWater * 1e9:F2} shed + "
                + $"{lens.TrailWater * 1e9:F2} trails");
            Assert.That(most, Is.LessThanOrEqualTo(LensWater.Capacity));
            Assert.That(longest, Is.LessThanOrEqualTo(LensWater.TrailPoints));
            Assert.That(books, Is.EqualTo(lens.ArrivedWater).Within(1e-9).Percent);
            Assert.That(lens.Shed, Is.GreaterThan(0));

            Fields(lens, 6000, 10, pitched);
            long before = AllocProbe.Before();
            Fields(lens, 6010, 100, pitched);
            long bytes = AllocProbe.After(before);
            Print($"{bytes} B over 100 fields");
            Assert.That(bytes, Is.Zero, AllocProbe.Report());
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Fields(lens, 6110, 500, pitched);
            Print($"{clock.Elapsed.TotalMilliseconds / 500:F3} ms a field with {lens.Drops.Length} drops");
        }

        static void Fields(LensWater lens, long from, int count, Quat attitude)
        {
            Double3 v = new Double3(0, 0, 20), air = attitude.InverseRotate(new Double3(0, 0, -26));
            for (long k = from; k < from + count; k++) Steady(lens, k, attitude, v, air);
        }
    }
}
