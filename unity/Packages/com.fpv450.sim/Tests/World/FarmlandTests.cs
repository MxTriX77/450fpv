using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The farmland's terrain behind IWorldQuery (w3-farmland-ground 1.1): maps/farmland's 17 tiles, loaded once. The heights
    // are checked to the bit against tools/map/validate.py (tools/map/golden/farmland-probes.txt, its output at the
    // package's 1,000 probes); the rest against the package's files read here, the W2 sign-off's measured numbers, and
    // closed forms on single facets.
    public sealed class FarmlandTests
    {
        private const double Exact = 1e-9;   // closed forms over coordinates of a few kilometres: only rounding
        private const double Advance = 1e-5; // conservative advancement stops within 1 µm of the surface
        private static MapWorld _world;

        internal static MapWorld World => _world ?? (_world = MapWorld.Load(MapWorldTests.Root, "farmland"));

        private static GroundSample Sample(double x, double z)
        {
            var g = new GroundSample[1];
            World.SampleGround(new[] { new XZ(x, z) }, g);
            return g[0];
        }

        private static RayHit Cast(Double3 o, Double3 d, double reach = 5)
        {
            var h = new RayHit[1];
            World.Raycast(new[] { new Ray(o, d) }, reach, h);
            return h[0];
        }

        private static double Slope(Double3 n) => Math.Acos(n.Y) * 180 / Math.PI;

        [Test]
        public void SampleGround_IsValidatePysHeightToTheBitAtTheThousandProbes()
        {
            string[] lines = File.ReadAllLines(Path.Combine(MapWorldTests.Root, "tools/map/golden/farmland-probes.txt"));
            int n = 0;
            foreach (string line in lines)
                n += line.StartsWith("#", StringComparison.Ordinal) ? 0 : 1;
            var points = new XZ[n];
            var want = new long[n];
            int k = 0;
            foreach (string line in lines)
            {
                if (line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                string[] p = line.Split(' ');
                points[k] = new XZ(Bits(p[0]), Bits(p[1]));
                want[k++] = long.Parse(p[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            Assert.That(n, Is.EqualTo(1000));
            var batch = new GroundSample[n];
            var one = new GroundSample[1];
            World.SampleGround(points, batch);
            int differ = 0, alone = 0;
            for (int i = 0; i < n; i++)
            {
                differ += BitConverter.DoubleToInt64Bits(batch[i].Height) == want[i] ? 0 : 1;
                World.SampleGround(new ReadOnlySpan<XZ>(points, i, 1), one);
                alone += Same(one[0], batch[i]) ? 0 : 1;
            }
            Assert.That(differ, Is.EqualTo(0), "probes whose height is not validate.py's to the bit");
            Assert.That(alone, Is.EqualTo(0), "probes whose one-by-one sample differs from the batch's");
        }

        private static double Bits(string hex) => BitConverter.Int64BitsToDouble(long.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

        private static bool Same(in GroundSample a, in GroundSample b) =>
            a.Height.Equals(b.Height) && a.Normal.X.Equals(b.Normal.X) && a.Normal.Y.Equals(b.Normal.Y) && a.Normal.Z.Equals(b.Normal.Z)
            && a.Surface == b.Surface && a.Flags == b.Flags;

        [Test]
        public void Load_DerivesTheTwelveStepLines_TheLedgeAndTheFarSeams()
        {
            Assert.That(World.QueryVersion, Is.EqualTo(9));
            Assert.That(World.PlacedObjects, Is.EqualTo(1679), "its trees, bushes, ruins and debris (1.2; FarmlandObjectTests)");
            TerrainStep[] steps = World.TerrainSteps;
            Assert.That(steps.Length, Is.EqualTo(12));
            int ledge = 0, seams = 0;
            foreach (TerrainStep s in steps)
            {
                Assert.That(s.C == -1000 || s.C == 2000, Is.True, "on the surround's outer edges");
                if (s.Rings == "far|surround")
                {
                    ledge++;
                    Assert.That((s.From, s.To), Is.EqualTo((-1000.0, 2000.0)));
                    Assert.That(s.Least, Is.GreaterThan(1.39), "the ledge: 1.399 m at least (W2's sign-off)");
                    Assert.That(s.Most, Is.LessThan(1.62), "the ledge: 1.610 m at most");
                }
                else
                {
                    seams++;
                    Assert.That(s.Rings, Is.EqualTo("far|far"));
                    Assert.That(s.To - s.From, Is.EqualTo(3000));
                    Assert.That(s.Most, Is.InRange(0.06, 0.1), "a far seam's steps: 70-99 mm (W2's sign-off)");
                }
            }
            Assert.That((ledge, seams), Is.EqualTo((4, 8)));
        }

        [Test]
        public void SampleGround_IsContinuousOverTheSharedSeamsAndStepsAtTheLedge()
        {
            for (double z = 3.3; z < 1000; z += 97.1)
            {
                foreach (double seam in new[] { 0.0, 1000.0 })
                    Assert.That(Math.Abs(Sample(seam - 1e-4, z).Height - Sample(seam + 1e-4, z).Height), Is.LessThan(1e-4), $"x = {seam}, z = {z}");
                double step = Sample(2000 - 1e-4, z).Height - Sample(2000 + 1e-4, z).Height;
                Assert.That(step, Is.InRange(1.39, 1.62), $"the ledge at z = {z}");
                Assert.That(Sample(2000, z).Height, Is.EqualTo(Sample(2000 - 1e-12, z).Height).Within(1e-6), "on the line, the top");
            }
        }

        [Test]
        public void SampleGround_OffTheWorldIsAHole()
        {
            foreach (XZ p in new[] { new XZ(5000.001, 0), new XZ(0, -4000.001), new XZ(1e7, 1e7), new XZ(double.NaN, 0) })
            {
                GroundSample g = Sample(p.X, p.Z);
                Assert.That((g.Flags, g.Surface), Is.EqualTo((GroundFlags.Hole, WorldIds.NoSurface)), $"({p.X}, {p.Z})");
                Assert.That(g.Height, Is.EqualTo(double.NegativeInfinity));
                Assert.That((g.Normal.X, g.Normal.Y, g.Normal.Z), Is.EqualTo((0.0, 1.0, 0.0)));
            }
            Assert.That(Sample(5000, 5000).Flags, Is.EqualTo(GroundFlags.None), "the world's edge is closed");
            Assert.That(Sample(-4000, -4000).Flags, Is.EqualTo(GroundFlags.None));
        }

        [Test]
        public void SampleGround_TheSurfaceIsTheNearestClassSampleOfItsTile()
        {
            // (file, x0, z0, size, resolution): the map, a surround tile and a far corner tile.
            var tiles = new (string File, double X0, double Z0, double Size, int Res)[]
            {
                ("classes/map.raw", 0, 0, 1000, 2048), ("classes/surround_NE.raw", 1000, 1000, 1000, 512), ("classes/far_SW.raw", -4000, -4000, 3000, 256),
            };
            var s = new Random(450);
            foreach (var t in tiles)
            {
                byte[] classes = File.ReadAllBytes(Path.Combine(MapWorldTests.Root, "maps/farmland", t.File));
                double spacing = t.Size / (t.Res - 1);
                for (int i = 0; i < 300; i++)
                {
                    double x = t.X0 + 1 + (t.Size - 2) * s.NextDouble(), z = t.Z0 + 1 + (t.Size - 2) * s.NextDouble();
                    // The nearest sample by distance: sample (c, r) at x0 + c·spacing, z0 + size − r·spacing (row 0 north).
                    int c = (int)Math.Round((x - t.X0) / spacing), r = (int)Math.Round((t.Z0 + t.Size - z) / spacing);
                    Assert.That(Sample(x, z).Surface, Is.EqualTo(classes[r * t.Res + c]), $"{t.File} at ({x}, {z})");
                }
            }
        }

        [Test]
        public void SampleGround_TheNormalIsTheFacetsAndTheVillageSlopesTo38Degrees()
        {
            // Inside each facet the height is linear: its central difference is the slope the normal stands on.
            const double s = 1000.0 / 1024, h = 1e-3;
            double steepest = 0;
            for (int cz = 600; cz < 650; cz++)
            {
                for (int cx = 500; cx < 570; cx++)
                {
                    foreach ((double u, double v) in new[] { (2.0 / 3, 1.0 / 3), (1.0 / 3, 2.0 / 3) })
                    {
                        double x = (cx + u) * s, z = (cz + v) * s;
                        GroundSample g = Sample(x, z);
                        double gx = (Sample(x + h, z).Height - Sample(x - h, z).Height) / (2 * h), gz = (Sample(x, z + h).Height - Sample(x, z - h).Height) / (2 * h);
                        double norm = Math.Sqrt(gx * gx + 1 + gz * gz);
                        Assert.That(g.Normal.X, Is.EqualTo(-gx / norm).Within(1e-7), $"({x}, {z})");
                        Assert.That(g.Normal.Y, Is.EqualTo(1 / norm).Within(1e-7));
                        Assert.That(g.Normal.Z, Is.EqualTo(-gz / norm).Within(1e-7));
                        Assert.That(g.Normal.X * g.Normal.X + g.Normal.Y * g.Normal.Y + g.Normal.Z * g.Normal.Z, Is.EqualTo(1).Within(1e-15));
                        steepest = Math.Max(steepest, Slope(g.Normal));
                    }
                }
            }
            Assert.That(steepest, Is.InRange(37.5, 37.7), "the levelled house plots beside the northern ruins (W2's 37.6°)");
        }

        [Test]
        public void Raycast_DownIsSampleGroundAndFromInsideNothing()
        {
            var s = new Random(7);
            for (int i = 0; i < 500; i++)
            {
                double x = -3990 + 8980 * s.NextDouble(), z = -3990 + 8980 * s.NextDouble();
                GroundSample g = Sample(x, z);
                RayHit hit = Cast(new Double3(x, g.Height + 3, z), new Double3(0, -1, 0));
                Assert.That(hit.Object, Is.EqualTo(WorldIds.Ground));
                Assert.That(hit.Distance, Is.EqualTo(3).Within(Exact), $"({x}, {z})");
                Assert.That((hit.Normal.X, hit.Normal.Y, hit.Normal.Z, hit.Surface), Is.EqualTo((g.Normal.X, g.Normal.Y, g.Normal.Z, g.Surface)));
                Assert.That(Cast(new Double3(x, g.Height - 0.5, z), new Double3(0, -1, 0)).Distance, Is.EqualTo(double.PositiveInfinity), "from inside");
                Assert.That(Cast(new Double3(x, g.Height + 0.5, z), new Double3(0, 1, 0)).Distance, Is.EqualTo(double.PositiveInfinity), "up and away");
            }
        }

        [Test]
        public void Raycast_AcrossTheLedgeHitsItsFace()
        {
            // Half a metre out over the far ring, at the face's mid-height, toward the surround: each of the 4 edges.
            var cases = new (Double3 Wall, Double3 Toward)[]
            {
                (new Double3(2000, 0, 500), new Double3(-1, 0, 0)), (new Double3(-1000, 0, 777), new Double3(1, 0, 0)),
                (new Double3(321, 0, 2000), new Double3(0, 0, -1)), (new Double3(1500, 0, -1000), new Double3(0, 0, 1)),
            };
            foreach (var c in cases)
            {
                Double3 inside = c.Wall + c.Toward * 1e-4, outside = c.Wall - c.Toward * 1e-4;
                double top = Sample(inside.X, inside.Z).Height, bottom = Sample(outside.X, outside.Z).Height;
                Double3 o = c.Wall - c.Toward * 0.5 + new Double3(0, 0.5 * (top + bottom), 0);
                RayHit hit = Cast(o, c.Toward);
                Assert.That(hit.Object, Is.EqualTo(WorldIds.Ground));
                Assert.That(hit.Distance, Is.EqualTo(0.5).Within(Exact), $"the face at ({c.Wall.X}, {c.Wall.Z})");
                Assert.That((hit.Normal.X, hit.Normal.Y, hit.Normal.Z), Is.EqualTo((-c.Toward.X, 0.0, -c.Toward.Z)), "horizontal, out of the surround");
                Assert.That(hit.Surface, Is.Not.EqualTo(WorldIds.NoSurface));
            }
        }

        [Test]
        public void StaticContacts_ASphereOnTheVillagesSteepestFacet()
        {
            // The facet of the steepest sample above: its centroid, a 15 mm sphere 1 mm into it, then 30 mm out of it.
            const double s = 1000.0 / 1024, r = 0.015;
            double best = 0, bx = 0, bz = 0;
            for (int cz = 600; cz < 650; cz++)
                for (int cx = 500; cx < 570; cx++)
                    foreach ((double u, double v) in new[] { (2.0 / 3, 1.0 / 3), (1.0 / 3, 2.0 / 3) })
                    {
                        double slope = Slope(Sample((cx + u) * s, (cz + v) * s).Normal);
                        if (slope > best)
                        {
                            best = slope;
                            bx = (cx + u) * s;
                            bz = (cz + v) * s;
                        }
                    }
            GroundSample g = Sample(bx, bz);
            var surface = new Double3(bx, g.Height, bz);
            var buffer = new StaticContact[4];
            Double3 into = surface + g.Normal * (r - 0.001);
            Assert.That(World.StaticContacts(new Capsule(into, into, r), new Capsule(into, into, r), 0.05, buffer), Is.EqualTo(1));
            StaticContact c = buffer[0];
            Assert.That((c.Object, c.Shape, c.Time, c.Surface), Is.EqualTo((WorldIds.Ground, 0, 1.0, g.Surface)));
            Assert.That(c.Distance, Is.EqualTo(-0.001).Within(Exact), $"on the {best:F1}° facet");
            Assert.That(Double3.Dot(c.Normal, g.Normal), Is.EqualTo(1).Within(1e-12));
            Assert.That((c.Point - surface).Length(), Is.LessThan(Exact));
            Double3 deep = surface - g.Normal * 0.004;
            Assert.That(World.StaticContacts(new Capsule(deep, deep, r), new Capsule(deep, deep, r), 0.05, buffer), Is.EqualTo(1));
            Assert.That(buffer[0].Distance, Is.EqualTo(-0.004 - r).Within(Exact), "4 mm into the slope, its own facet's plane");
            Double3 from = surface + g.Normal * (r + 0.03), to = surface + g.Normal * (r - 0.01);
            Assert.That(World.StaticContacts(new Capsule(from, from, r), new Capsule(to, to, r), 0.05, buffer), Is.EqualTo(1));
            Assert.That(buffer[0].Time, Is.EqualTo(0.75).Within(Advance), "falling along the normal: the first touch");
            Assert.That(buffer[0].Distance, Is.EqualTo(0).Within(Advance));
        }

        [Test]
        public void StaticContacts_ASweepAcrossTheLedgeTouchesItsFace()
        {
            const double r = 0.015;
            double top = Sample(2000 - 1e-4, 500).Height, bottom = Sample(2000 + 1e-4, 500).Height, y = 0.5 * (top + bottom);
            var buffer = new StaticContact[4];
            int n = World.StaticContacts(new Capsule(new Double3(2000.1, y, 500), new Double3(2000.1, y, 500), r),
                new Capsule(new Double3(1999.9, y, 500), new Double3(1999.9, y, 500), r), 0.05, buffer);
            Assert.That(n, Is.EqualTo(1));
            Assert.That(buffer[0].Time, Is.EqualTo((0.1 - r) / 0.2).Within(Advance), "a wall contact, never a jump through the step");
            Assert.That(buffer[0].Point.X, Is.EqualTo(2000).Within(Exact));
            Assert.That((buffer[0].Normal.X, buffer[0].Normal.Y, buffer[0].Normal.Z), Is.EqualTo((1.0, 0.0, 0.0)));
        }

        // ---- Determinism -----------------------------------------------------------------------------------------------

        /// <summary>One hash over a seeded set of samples, rays and swept capsules near the ground, everywhere on the world.</summary>
        private static ulong RunQueries(MapWorld world, int seed, int count)
        {
            var s = new Random(seed);
            var g = new GroundSample[1];
            var hit = new RayHit[1];
            var contacts = new StaticContact[8];
            var snags = new SnagSegment[64];
            ulong hash = 0xCBF29CE484222325UL;
            void Add(double v)
            {
                ulong bits = (ulong)BitConverter.DoubleToInt64Bits(v);
                for (int k = 0; k < 8; k++)
                    hash = (hash ^ (byte)(bits >> 8 * k)) * 0x100000001B3UL;
            }
            for (int i = 0; i < count; i++)
            {
                double x = i % 3 == 0 ? 1000 * s.NextDouble() : -4100 + 9200 * s.NextDouble(), z = i % 3 == 0 ? 1000 * s.NextDouble() : -4100 + 9200 * s.NextDouble();
                world.SampleGround(new[] { new XZ(x, z) }, g);
                Add(g[0].Height); Add(g[0].Normal.X); Add(g[0].Normal.Y); Add(g[0].Surface);
                double y = double.IsInfinity(g[0].Height) ? 20 : g[0].Height;
                var o = new Double3(x, y + 0.3 * s.NextDouble() - 0.05, z);
                var d = new Double3(s.NextDouble() - 0.5, -s.NextDouble(), s.NextDouble() - 0.5);
                world.Raycast(new[] { new Ray(o, d) }, 3, hit);
                Add(hit[0].Distance); Add(hit[0].Point.X); Add(hit[0].Normal.Y); Add(hit[0].Surface);
                var a = new Double3(x, y + 0.2 * s.NextDouble() - 0.02, z);
                var b = a + new Double3(0.3 * s.NextDouble(), 0.05 * s.NextDouble(), 0.3 * s.NextDouble());
                var move = new Double3(0.06 * s.NextDouble() - 0.03, -0.06 * s.NextDouble(), 0.06 * s.NextDouble() - 0.03);
                int n = world.StaticContacts(new Capsule(a, b, 0.01), new Capsule(a + move, b + move, 0.01), 0.05, contacts);
                Add(n);
                for (int k = 0; k < Math.Min(n, contacts.Length); k++)
                {
                    Add(contacts[k].Distance); Add(contacts[k].Time); Add(contacts[k].Point.Y); Add(contacts[k].Normal.X); Add(contacts[k].Surface);
                    Add(contacts[k].Object); Add(contacts[k].Shape);
                }
                n = world.SnagsNear(a + new Double3(0, s.NextDouble(), 0), 0.5 * s.NextDouble(), snags);
                Add(n);
                for (int k = 0; k < Math.Min(n, snags.Length); k++)
                {
                    Add(snags[k].Object); Add(snags[k].Segment); Add(snags[k].A.X); Add(snags[k].B.Y); Add(snags[k].Radius);
                }
            }
            return hash;
        }

        [Test]
        public void Concurrency_FourThreadsOnTheFarmlandGetTheBitsOfOne()
        {
            MapWorld world = World;
            const int count = 3000;
            ulong alone = RunQueries(world, 11, count);
            var results = new ulong[4];
            var errors = new Exception[4];
            var threads = new Thread[4];
            for (int t = 0; t < threads.Length; t++)
            {
                int me = t;
                threads[t] = new Thread(() =>
                {
                    try
                    {
                        for (int round = 0; round < 3; round++)
                            results[me] = RunQueries(world, 11, count);
                    }
                    catch (Exception e)
                    {
                        errors[me] = e;
                    }
                });
            }
            foreach (Thread thread in threads)
                thread.Start();
            foreach (Thread thread in threads)
                thread.Join();
            for (int t = 0; t < threads.Length; t++)
            {
                Assert.That(errors[t], Is.Null, $"thread {t}");
                Assert.That(results[t], Is.EqualTo(alone), $"thread {t}");
            }
        }

        [Test]
        public void Queries_AllocateNothingAfterTheFirst()
        {
            MapWorld world = World;
            var points = new XZ[44];
            var samples = new GroundSample[44];
            var rays = new Ray[8];
            var hits = new RayHit[8];
            var contacts = new StaticContact[16];
            var snags = new SnagSegment[256];
            var s = new Random(3);
            for (int i = 0; i < points.Length; i++)
                points[i] = new XZ(500 + 20 * s.NextDouble(), 600 + 20 * s.NextDouble());
            double y = Sample(510, 610).Height;
            for (int i = 0; i < rays.Length; i++)
                rays[i] = new Ray(new Double3(505 + i, y + 2, 610), new Double3(0, i % 2 == 0 ? -1 : 1, 0));
            var a = new Double3(510, y + 0.01, 610);
            void Step()
            {
                world.SampleGround(points, samples);
                world.Raycast(rays, 5, hits);
                for (int i = 0; i < 46; i++)
                    world.StaticContacts(new Capsule(a, a + new Double3(0.1, 0, 0.01 * i), 0.005), new Capsule(a, a + new Double3(0.1, -0.01, 0.01 * i), 0.005), 0.1, contacts);
                world.SnagsNear(a + new Double3(0, 1, 0), 2, snags);
            }
            Step();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 100; k++)
                Step();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            // No constraint built in this method: in Release the JIT boxed Is.EqualTo(0)'s literal before the last read, the
            // 24 B this test reported since 1.1 (a bare pair of reads with nothing between them measured it too).
            if (allocated != 0)
                Assert.Fail($"{allocated} B over 100 steps among the village's ruins and debris (500-520, 600-620)");
        }
    }
}
