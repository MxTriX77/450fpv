using System;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7c-wind 1.3's wake grid over its synthetic layout (WakeGridFixture), wind from the north unless a test says:
    // the obstacle grid follows K§10.3; the belt's deficit is Perera's and its turbulence about 2× from 5 to 15 H in a
    // wake 2–3 H tall (K§6.7, K§8.2); the solid house recirculates within 2–3 H and the porous belt nowhere; a jet runs
    // through the belt's gap; the grid's bits depend only on the obstacles and the direction, the worker hands over at
    // fixed steps, and sampling allocates nothing.
    public sealed class WakeGridTests
    {
        const double H = WakeGridFixture.MapleTop;

        static WakeObstacles _obstacles;
        static WakeGrid _north;

        static WakeObstacles Obstacles() => _obstacles ?? (_obstacles = WakeGridFixture.Obstacles());

        static WakeGrid North() => _north ?? (_north = WakeGrid.Build(Obstacles(), 0));

        static int Cell(double v) => (int)((v + 300) / 2);

        /// <summary>The cell south of the belt (wind from the north) whose obstacle is nearest x/H = X, in column x.</summary>
        static int RowBehindBelt(double x, double X)
        {
            int best = -1;
            double err = double.PositiveInfinity;
            for (int j = Cell(WakeGridFixture.BeltLeeZ); j >= 0; j--)
            {
                North().Describe(Cell(x), j, out double cx, out double h, out _, out _, out _, out _, out _);
                if (h > 0 && Math.Abs(cx - X) < err) { err = Math.Abs(cx - X); best = j; }
            }
            return best;
        }

        /// <summary>Perera (1981)'s R as the European Wind Atlas writes it, with System.Math (the reference).</summary>
        static double Perera(double X, double Z, double beta, double h)
        {
            double k = 2 * 0.4 * 0.4 / Math.Log(h / 0.03);
            double eta = Math.Max(Z * Math.Pow(k * X, -1 / 2.14), 0.99667);
            return 9.75 * (1 - beta) * eta * Math.Exp(-0.67 * Math.Pow(eta, 1.5)) / X;
        }

        [Test]
        public void TheObstacleGridFollowsK10_3()
        {
            WakeObstacles o = Obstacles();
            WakeObstacles.Lane lane = o.NewLane();
            // A cell well inside a maple's crown: top 8.4 m, base 2.2 m. A lane through the westmost maple's centre, the wind
            // from the north: each line passes 0.45^(chord / 6.2 m), its chord 2·√(r² − y²) (w7-farmland-wind 1.2).
            int k = o.CellAt(-147.5 + 0.5, 0.5);
            Assert.That(o.Top(k), Is.EqualTo(8.4));
            Assert.That(o.Base(k), Is.EqualTo(2.2));
            double expected = 0;
            for (int l = 0; l < WakeObstacles.LaneLines; l++)
            {
                double y = ((l + 0.5) / WakeObstacles.LaneLines - 0.5) * WakeObstacles.Cell;
                expected += Math.Pow(0.45, 2 * Math.Sqrt(3.1 * 3.1 - y * y) / 6.2) / WakeObstacles.LaneLines;
            }
            Assert.That(o.Transmission(lane, -147.5, -3, 0, 1, 1, 0, double.NegativeInfinity, 2.2, 8.4), Is.EqualTo(expected).Within(1e-12));
            // The house is solid: a lane through it passes nothing, from the ground to 5 m.
            k = o.CellAt(WakeGridFixture.HouseX, WakeGridFixture.HouseZ);
            Assert.That(o.Top(k), Is.EqualTo(5));
            Assert.That(o.Base(k), Is.EqualTo(0));
            Assert.That(o.Transmission(lane, WakeGridFixture.HouseX, WakeGridFixture.HouseZ - 2.5, 0, 1, 1, 0, double.NegativeInfinity, 0, 5), Is.EqualTo(0));
            // Groups: the belt's two segments either side of the gap, and the house.
            Assert.That(o.Groups, Is.EqualTo(3));
            Assert.That(o.Group(o.CellAt(0, 0)), Is.Not.EqualTo(o.Group(o.CellAt(100, 0))));
            Assert.That(o.IsObstacle(o.CellAt(WakeGridFixture.GapMiddle, 0)), Is.False, "the gap is open");
            // A path across a crown keeps about the volume's own porosity (the K§10.3 rule's intent).
            North().Describe(Cell(-60), RowBehindBelt(-60, 5), out _, out _, out _, out double beta, out _, out _, out _);
            Assert.That(beta, Is.InRange(0.40, 0.50));
        }

        [Test]
        public void AThinWallBlocksItsLaneAndAPartUnderACrownStandsBesideIt()
        {
            // w7-farmland-wind 1.2: a solid wall 0.24 m thick between the old rule's 4 × 4 samples (z 0.28–0.52 in its cell),
            // 10 m along X, 3 m tall; a trunk 0.3 m square, 0–2 m, under a crown box 4 m square, 2–6 m, porosity 0.5.
            var volumes = new[]
            {
                new WindVolume { Kind = WindVolumeKind.Box, X = 0, Z = 0.40, BaseM = 0, TopM = 3, HalfX = 5, HalfZ = 0.12, Porosity = 0 },
                new WindVolume { Kind = WindVolumeKind.Box, X = 31, Z = 31, BaseM = 0, TopM = 2, HalfX = 0.15, HalfZ = 0.15, Porosity = 0 },
                new WindVolume { Kind = WindVolumeKind.Box, X = 31, Z = 31, BaseM = 2, TopM = 6, HalfX = 2, HalfZ = 2, Porosity = 0.5 },
            };
            var o = new WakeObstacles(-50, -50, 50, 50, volumes);
            WakeObstacles.Lane lane = o.NewLane();
            int wall = o.CellAt(0, 0.4);
            Assert.That(o.IsObstacle(wall), Is.True, "the thin wall is never lost between samples");
            // Across it (the wind from the north) the lane is opaque; along it (from the east) one line in eight meets it.
            Assert.That(o.Transmission(lane, 0, 1, 0, 1, 1, 0, double.NegativeInfinity, 0, 3), Is.EqualTo(0));
            Assert.That(o.Transmission(lane, 1, 1, 1, 0, 0, -1, double.NegativeInfinity, 0, 3), Is.EqualTo(7.0 / 8).Within(1e-15));
            // The trunk blocks 2 lines of 8 below the crown; the crown passes 0.5^(4 m / D), D = 16 m / π, on every line above:
            // the two heights stand side by side, never in series.
            double crown = Math.Pow(0.5, 4 / (16 / Math.PI));
            double expected = (2 * (6.0 / 8) + 4 * crown) / 6;
            Assert.That(o.Transmission(lane, 31, 29.5, 0, 1, 1, 0, double.NegativeInfinity, 0, 6), Is.EqualTo(expected).Within(1e-12));
            Assert.That(expected, Is.GreaterThan(crown * 6.0 / 8 + 0.1), "more open than trunk and crown in series");
        }

        [Test]
        public void ATallerObstaclesWakeGoesOnOverALowerOneBehindIt()
        {
            // w7-farmland-wind 1.2: a row of crowns 10 m tall (box 40 × 6 m, porosity 0.1 over its mean width, so a lane across it
            // passes 0.1^(6 / 29.3) = 0.62) and a hedge 2 m tall 15 m behind it, the wind from the north. Behind the hedge its wake
            // is the nearest; the row's goes on over it.
            WindVolume row = new WindVolume { Kind = WindVolumeKind.Box, X = 0, Z = 40, BaseM = 0, TopM = 10, HalfX = 20, HalfZ = 3, Porosity = 0.1 };
            WindVolume hedge = new WindVolume { Kind = WindVolumeKind.Box, X = 0, Z = 22, BaseM = 0, TopM = 2, HalfX = 20, HalfZ = 1, Porosity = 0.5 };
            WakeGrid both = WakeGrid.Build(new WakeObstacles(-100, -100, 100, 100, new[] { row, hedge }), 0);
            WakeGrid alone = WakeGrid.Build(new WakeObstacles(-100, -100, 100, 100, new[] { row }), 0);
            int i = 50, j = 50; // the cell (0..2, 0..2): 20 m behind the hedge, 34 m behind the row
            both.Describe(i, j, out _, out double near, out _, out _, out _, out _, out _);
            both.DescribeOver(i, j, out double x2, out double over, out _, out _);
            Assert.That(near, Is.EqualTo(2), "the hedge's wake is the nearest");
            Assert.That(over, Is.EqualTo(10), "the row's goes on over it");
            alone.Describe(i, j, out double x1, out _, out _, out _, out _, out _, out _);
            Assert.That(x2, Is.EqualTo(x1), "at the row's own distance");
            foreach (double z in new[] { 1.0, 3, 6, 9, 14 })
            {
                both.SampleCell(i, j, z, out WakeSample s);
                both.SampleNearest(i, j, z, out WakeSample n);
                alone.SampleCell(i, j, z, out WakeSample r);
                Assert.That(s.Speed, Is.EqualTo(n.Speed * r.Speed), $"the speeds multiply at {z} m");
                double excess = (n.Turbulence * n.Turbulence - 1) + (r.Turbulence * r.Turbulence - 1);
                Assert.That(s.Turbulence * s.Turbulence - 1, Is.EqualTo(excess).Within(1e-12), $"the excess variances add at {z} m");
            }
            both.SampleCell(i, j, 6, out WakeSample at6);
            both.SampleNearest(i, j, 6, out WakeSample hedgeAlone);
            Assert.That(hedgeAlone.Speed, Is.GreaterThan(0.99), "the known-wrong: the hedge's wake alone is gone by 3 of its heights");
            Assert.That(at6.Speed, Is.LessThan(0.8), "the row's shelter at 6 m");
        }

        [Test]
        public void TheBeltsDeficitIsPererasShelterModel()
        {
            WakeGrid g = North();
            foreach (double target in new[] { 5.0, 7.5, 10, 15, 20, 25 })
            {
                int j = RowBehindBelt(-60, target);
                g.Describe(Cell(-60), j, out double X, out double h, out double zb, out double beta, out double phi, out _, out _);
                Assert.That(h, Is.EqualTo(H), "the belt's height");
                Assert.That(zb, Is.EqualTo(2.2 / 8.4).Within(1e-12), "the crowns' base");
                Assert.That(phi, Is.GreaterThan(0.99), "far from the belt's ends");
                foreach (double Z in new[] { 0.5, 1.0, 1.5, 2.0 })
                {
                    g.SampleCell(Cell(-60), j, Z * h, out WakeSample s);
                    double expected = 1 - phi * Perera(X, Z, beta, h);
                    if (Z >= 1.5) expected += 0; // the speed-up's Gaussian is gone by X = 5 (exp(−11))
                    Assert.That(s.Speed, Is.EqualTo(expected).Within(2e-5), $"x/H {X:F2}, z/H {Z}");
                }
            }
            // The shelter: about half the wind at 5 H, recovering to within 15 % by 20 H.
            g.SampleCell(Cell(-60), RowBehindBelt(-60, 5), 0.5 * H, out WakeSample at5);
            g.SampleCell(Cell(-60), RowBehindBelt(-60, 20), 0.5 * H, out WakeSample at20);
            Assert.That(at5.Speed, Is.InRange(0.4, 0.6));
            Assert.That(at20.Speed, Is.InRange(0.85, 0.9));
        }

        [Test]
        public void TheBeltDoublesTheTurbulenceFrom5To15HInAWake2To3HTall()
        {
            WakeGrid g = North();
            double sum = 0;
            int n = 0;
            for (int j = Cell(WakeGridFixture.BeltLeeZ); j >= 0; j--)
            {
                g.Describe(Cell(-60), j, out double X, out _, out _, out _, out _, out _, out _);
                if (X < 5 || X > 15) continue;
                g.SampleCell(Cell(-60), j, 0.5 * H, out WakeSample s);
                Assert.That(s.Turbulence, Is.InRange(1.7, 2.3), $"x/H {X:F2}");
                sum += s.Turbulence;
                n++;
            }
            Assert.That(sum / n, Is.EqualTo(2.0).Within(0.1), "the literature's about 2x (K§8.2)");

            // The quiet zone just behind it, and the recovery by 30 H.
            g.SampleCell(Cell(-60), RowBehindBelt(-60, 1), 0.5 * H, out WakeSample quiet);
            g.SampleCell(Cell(-60), RowBehindBelt(-60, 30), 0.5 * H, out WakeSample far);
            Assert.That(quiet.Turbulence, Is.LessThan(1.15));
            Assert.That(far.Turbulence, Is.LessThan(1.15));

            // The wake's top (where the boost falls to a tenth of its low-level value) grows, and lies at 2–3 H at 10–20 H.
            double previous = 0;
            foreach (double X in new[] { 5.0, 10, 15, 20 })
            {
                int j = RowBehindBelt(-60, X);
                g.SampleCell(Cell(-60), j, 0.5 * H, out WakeSample low);
                double top = 0;
                for (double Z = 0.5; Z < 6; Z += 0.01)
                {
                    g.SampleCell(Cell(-60), j, Z * H, out WakeSample s);
                    if (s.Turbulence - 1 < 0.1 * (low.Turbulence - 1)) { top = Z; break; }
                }
                Assert.That(top, Is.GreaterThan(previous), $"the wake grows at x/H {X}");
                if (X >= 10) Assert.That(top, Is.InRange(2.0, 3.0), $"the wake's top at x/H {X} (K§8.2: 2–3 H)");
                previous = top;
            }
        }

        [Test]
        public void TheSolidHouseRecirculatesWithin2To3HAndTheBeltNowhere()
        {
            WakeGrid g = North();
            double xr = WakeGrid.Reattachment(5, 10, 6);
            Assert.That(xr, Is.InRange(2, 3), "Fackrell's reattachment for a 10 x 6 x 5 m house");
            double first = double.PositiveInfinity, last = 0, strongest = 0;
            for (int j = Cell(WakeGridFixture.HouseLeeZ); j >= Cell(WakeGridFixture.HouseLeeZ - 60); j--)
            {
                if (-300 + 2 * j + 1 > WakeGridFixture.HouseLeeZ) continue;
                g.Describe(Cell(WakeGridFixture.HouseX), j, out double X, out double h, out _, out double beta, out _, out _, out double cavity);
                Assert.That(h, Is.EqualTo(5), "the house's height");
                Assert.That(beta, Is.EqualTo(0), "the house is solid");
                for (double Z = 0.05; Z < 1.5; Z += 0.05)
                {
                    g.SampleCell(Cell(WakeGridFixture.HouseX), j, Z * h, out WakeSample s);
                    if (!(s.Speed < 0)) continue;
                    first = Math.Min(first, X);
                    last = Math.Max(last, X);
                    strongest = Math.Min(strongest, s.Speed);
                }
            }
            Assert.That(first, Is.LessThan(0.5), "reverse flow starts at the leeward face");
            Assert.That(last, Is.InRange(1.5, 3.0), "and ends within 2–3 H (K§6.7)");
            Assert.That(last, Is.LessThan(xr));
            Assert.That(strongest, Is.InRange(-0.4, -0.2), "about −0.3 of the local mean");

            // The porous belt (β ≈ 0.47 > 0.3) has no reverse flow anywhere behind it.
            for (int j = 0; j <= Cell(WakeGridFixture.BeltLeeZ); j++)
                for (int i = Cell(-150); i <= Cell(150); i += 3)
                    foreach (double Z in new[] { 0.05, 0.25, 0.5, 1.0 })
                    {
                        g.SampleCell(i, j, Z * H, out WakeSample s);
                        Assert.That(s.Speed, Is.GreaterThan(0), $"cell ({i}, {j}) at z/H {Z}");
                    }
        }

        [Test]
        public void AJetRunsThroughTheBeltsGap()
        {
            WakeGrid g = North();
            int gap = Cell(WakeGridFixture.GapMiddle), belt = Cell(-60);
            // Just behind the belt (1–2 m to 1 H): faster than the open field in the gap, sheltered behind the trees.
            for (double z = WakeGridFixture.BeltLeeZ - 2; z >= WakeGridFixture.BeltLeeZ - H; z -= 2)
            {
                g.SampleCell(gap, Cell(z), 0.5 * H, out WakeSample inGap);
                g.SampleCell(belt, Cell(z), 0.5 * H, out WakeSample behind);
                Assert.That(inGap.Speed, Is.GreaterThan(1.03), $"the jet at z {z}");
                Assert.That(behind.Speed, Is.LessThan(0.6), $"the shelter at z {z}");
            }
            // The jet decays and the side wakes fill the lane in downstream (lateral spread).
            g.SampleCell(gap, Cell(WakeGridFixture.BeltLeeZ - 1), 0.5 * H, out WakeSample near);
            g.SampleCell(gap, Cell(WakeGridFixture.BeltLeeZ - 10 * H), 0.5 * H, out WakeSample tenH);
            Assert.That(tenH.Speed, Is.LessThan(near.Speed));
            Assert.That(tenH.Speed, Is.LessThan(1), "by 10 H the lane is sheltered again");
            // The interpolated field is continuous across the lane's edges (no step at a cell border).
            double last = double.NaN;
            for (double x = 40; x <= 75; x += 0.05)
            {
                g.Sample(x, WakeGridFixture.BeltLeeZ - 6, 0.5 * H, out WakeSample s);
                if (!double.IsNaN(last)) Assert.That(Math.Abs(s.Speed - last), Is.LessThan(0.03), $"at x {x:F2}");
                last = s.Speed;
            }
        }

        [Test]
        public void TheGridIsAFunctionOfTheObstaclesAndTheDirectionOnly()
        {
            foreach (double deg in new[] { 0.0, 10, 37, 215.5 })
            {
                WakeGrid a = WakeGrid.Build(Obstacles(), deg);
                WakeGrid b = WakeGrid.Build(WakeGridFixture.Obstacles(), deg);
                Assert.That(b.Hash(), Is.EqualTo(a.Hash()), $"two builds at {deg} deg");
                WakeGrid spare = WakeGrid.Build(Obstacles(), deg + 90);
                spare.Rebuild(deg);
                Assert.That(spare.Hash(), Is.EqualTo(a.Hash()), $"a rebuild in place at {deg} deg");
            }
            Assert.That(WakeGrid.Build(Obstacles(), 10).Hash(), Is.Not.EqualTo(North().Hash()));
            // Turned 90 degrees the belt runs along the wind and its wake is gone from the south side.
            WakeGrid east = WakeGrid.Build(Obstacles(), 90);
            east.SampleCell(Cell(-60), RowBehindBelt(-60, 5), 0.5 * H, out WakeSample s);
            Assert.That(s.Speed, Is.EqualTo(1));
        }

        [Test]
        public void TheWorkerHandsOverAtFixedStepsWithTheDirectBuildsBits()
        {
            using var worker = new WakeGridWorker(Obstacles(), 0);
            Assert.That(worker.Active.Hash(), Is.EqualTo(North().Hash()));
            for (long s = 0; s < 100; s++) worker.Step(s, 4.9); // within 5 degrees: no request
            Assert.That(worker.Pending, Is.False);
            long request = 100, takeover = request + WakeGridWorker.LatencySteps;
            for (long s = 100; s < takeover; s++)
            {
                worker.Step(s, 10);
                Assert.That(worker.Pending, Is.True);
                Assert.That(worker.Active.FromDeg, Is.EqualTo(0));
            }
            worker.Step(takeover, 10);
            Assert.That(worker.Fading, Is.Not.Null);
            Assert.That(worker.Fading.FromDeg, Is.EqualTo(10));
            Assert.That(worker.Fading.Hash(), Is.EqualTo(WakeGrid.Build(Obstacles(), 10).Hash()));

            // Half way through the fade the sample is the two grids' blend.
            for (long s = takeover + 1; s <= takeover + WakeGridWorker.FadeSteps / 2; s++) worker.Step(s, 10);
            Assert.That(worker.FadeWeight, Is.EqualTo(0.5));
            double x = 20, z = -30, y = 4;
            worker.Sample(x, z, y, out WakeSample blend);
            worker.Active.Sample(x, z, y, out WakeSample a);
            worker.Fading.Sample(x, z, y, out WakeSample b);
            Assert.That(blend.Speed, Is.EqualTo(a.Speed + 0.5 * (b.Speed - a.Speed)));

            for (long s = takeover + WakeGridWorker.FadeSteps / 2 + 1; s <= takeover + WakeGridWorker.FadeSteps; s++) worker.Step(s, 10);
            Assert.That(worker.Fading, Is.Null);
            Assert.That(worker.Active.FromDeg, Is.EqualTo(10));
            Assert.That(worker.Builds, Is.EqualTo(1));
        }

        [Test]
        public void StepAndSampleAllocateNothing()
        {
            using var worker = new WakeGridWorker(Obstacles(), 0);
            WakeSample s = default;
            double sum = 0;
            // Warm up with one whole cycle (request, a possible stall, takeover, fade): first calls may allocate.
            for (long step = 0; step < 6000; step++)
            {
                worker.Step(step, step < 10 ? 0 : 12);
                worker.Sample(step * 0.01 - 50, -20, 10, out s);
            }
            // Each step's Step and samples, measured step by step (the counter read after the step's last sample), so
            // the loop's own exit (the runtime's tiering) is not counted; a request, the stall and the fade are inside.
            long allocated = 0, firstAlloc = -1;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (long step = 6000; step < 12000; step++)
            {
                worker.Step(step, step < 6010 ? 12 : 24);
                for (int p = 0; p < 38; p++)
                {
                    worker.Sample(-80 + 0.013 * step + 0.2 * p, -30, 3 + p, out s);
                    sum += s.Speed;
                }
                long now = GC.GetAllocatedBytesForCurrentThread();
                if (now != before && firstAlloc < 0) { firstAlloc = step; allocated = now - before; }
            }
            Assert.That(allocated, Is.EqualTo(0), "first at step " + firstAlloc);
            Assert.That(worker.Builds, Is.EqualTo(2));
            Assert.That(sum, Is.Not.NaN);
        }

        [Test]
        public void TheExpAndLnAreAccurate()
        {
            foreach (double x in new[] { -700, -30, -2.5, -0.5, -1e-9, 0, 0.3, 1, 7.7, 300 })
                Assert.That(WakeGridMath.Exp(x), Is.EqualTo(Math.Exp(x)).Within(4e-16 * Math.Exp(x) + 1e-300), $"exp {x}");
            foreach (double x in new[] { 1e-300, 1e-5, 0.03, 0.5, 1, 1.4142, 2, 280, 1e200 })
                Assert.That(WakeGridMath.Ln(x), Is.EqualTo(Math.Log(x)).Within(4e-16 * Math.Abs(Math.Log(x)) + 1e-16), $"ln {x}");
            Assert.That(WakeGridMath.Phi(0), Is.EqualTo(0.5).Within(2e-7));
            Assert.That(WakeGridMath.Phi(1.959964), Is.EqualTo(0.975).Within(2e-7));
            Assert.That(WakeGridMath.Pow(0, 0.3), Is.EqualTo(0));
        }
    }
}
