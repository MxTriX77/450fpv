using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Fpv450.Sim;
using Fpv450.Sim.World;

namespace Fpv450.WorldBench
{
    // The farmland's cases (w3-farmland-ground 1.1, 1.2): maps/farmland's terrain and its 1,679 trees, bushes, ruins and debris
    // behind MapWorld.
    // - Golden: the 1,000 height probes against validate.py's bits (tools/map/golden/farmland-probes.txt), the 12 derived
    //   steps, then 10 000 each of ground samples, swept capsules and rays near the ground over its spots (the village's
    //   steep plots, a field, the map's seam, the ledge, a far seam, the far ring, the world's edge; input i in spot i mod 7),
    //   batched against one by one where a call takes a batch. Then the objects (1.2): every placed shape and wind volume, and
    //   10 000 each of swept capsules, rays and snag spheres among them (the village's ruins and debris, the F6|F7 belt, a
    //   grove, a bush cluster; input i at spot i mod 4, near a shape of an object standing there).
    // - Bench: a physics step's queries along paths low over the farmland and through its objects, in physics' pattern and
    //   K§10.5's, at physics' margin; then 100 000 samples, capsules and rays; a laid fiber segment's query by its length,
    //   the ground left out as the fiber leaves it (2.1); and SnagsNear at the fiber caches' radii (shown: off the step).
    //   Judged as D-054 sets it for the test range: physics' pattern inside PLT-04's 300 µs step at p99 with 0 B, PLT-04's
    //   60 µs line for the world queries printed beside it (D-061's target) with the max; K§10.5's batch with its fiber shown.
    internal static partial class Program
    {
        private static MapWorld Farmland() => MapWorld.Load(RepositoryRoot(), "farmland");

        private static double GroundAt(IWorldQuery world, double x, double z)
        {
            var g = new GroundSample[1];
            world.SampleGround(new[] { new XZ(x, z) }, g);
            return g[0].Height;
        }

        /// <summary>A box of the farmland in x–z with a height band over a reference ground (its own, or `from`'s), m.</summary>
        private readonly struct Patch
        {
            public readonly string Name;
            public readonly double X0, Z0, X1, Z1, Low, High;
            /// <summary>When set, heights are over the ground at this x (or z) offset of the point: the ledge's lower side.</summary>
            public readonly double FromX, FromZ;

            public Patch(string name, double x0, double z0, double x1, double z1, double low, double high, double fromX = double.NaN, double fromZ = double.NaN)
            {
                Name = name;
                X0 = x0;
                Z0 = z0;
                X1 = x1;
                Z1 = z1;
                Low = low;
                High = high;
                FromX = fromX;
                FromZ = fromZ;
            }

            public Double3 Draw(IWorldQuery world, ref Stream s)
            {
                double x = s.In(X0, X1), z = s.In(Z0, Z1);
                double g = GroundAt(world, double.IsNaN(FromX) ? x : FromX, double.IsNaN(FromZ) ? z : FromZ);
                return new Double3(x, (double.IsInfinity(g) ? 20 : g) + s.In(Low, High), z);
            }
        }

        /// <summary>The object spots (1.2): a box in x–z, its objects those whose first shape stands in it.</summary>
        private static readonly (string Name, double X0, double Z0, double X1, double Z1)[] ObjectSpots =
        {
            ("the village's ruins and debris", 465, 573, 550, 630),
            ("the F6|F7 belt", 295, 750, 400, 770),
            ("the grove", 415, 422, 445, 448),
            ("a bush cluster", 100, 250, 150, 300),
        };

        /// <summary>Each object spot's objects, in object order.</summary>
        private static int[][] SpotObjects(MapWorld world)
        {
            var spots = new int[ObjectSpots.Length][];
            for (int k = 0; k < spots.Length; k++)
            {
                var list = new List<int>();
                for (int o = 0; o < world.PlacedObjects; o++)
                    if (world.Geometry(o, 0, out ShapeGeometry g) && g.Center.X >= ObjectSpots[k].X0 && g.Center.X <= ObjectSpots[k].X1
                        && g.Center.Z >= ObjectSpots[k].Z0 && g.Center.Z <= ObjectSpots[k].Z1)
                        list.Add(o);
                spots[k] = list.ToArray();
            }
            return spots;
        }

        /// <summary>A point near a random shape of a random object of the spot: its centre and up to `reach` off it.</summary>
        private static Double3 NearShape(MapWorld world, int[] objects, ref Stream s, double reach)
        {
            int o = objects[(int)(s.Next() * objects.Length)], shapes = 0;
            while (world.Geometry(o, shapes, out _))
                shapes++;
            world.Geometry(o, (int)(s.Next() * shapes), out ShapeGeometry g);
            return g.Center + s.Move(reach);
        }

        private static Patch[] FarmlandPatches() => new[]
        {
            new Patch("village plots", 495, 600, 560, 640, -0.1, 1.0),
            new Patch("field", 200, 200, 260, 260, -0.1, 1.0),
            new Patch("map seam", 997, 400, 1003, 420, -0.1, 1.0),
            new Patch("ledge", 1999, 500, 2001, 520, -0.1, 1.9, fromX: 2000.0001),
            new Patch("far seam", 3000, -1001, 3010, -999, -0.1, 0.4),
            new Patch("far ring", -3000, 3000, -2900, 3100, -0.1, 1.0),
            new Patch("world's edge", 4990, 0, 5010, 10, -0.1, 1.0),
        };

        // ---- Golden --------------------------------------------------------------------------------------------------

        private static List<(string Name, string Value, bool Agree)> FarmlandCases()
        {
            MapWorld world = Farmland();
            var cases = new List<(string, string, bool)>
            {
                ("query_version", world.QueryVersion.ToString(CultureInfo.InvariantCulture), true),
                ("content_hash", Hex(world.ContentHash), true),
            };

            // validate.py's heights at the probes, to the bit (the file's bits are the reference; the hash records the run's).
            var probes = new List<XZ>();
            var want = new List<long>();
            foreach (string line in File.ReadAllLines(Path.Combine(RepositoryRoot(), "tools", "map", "golden", "farmland-probes.txt")))
            {
                if (line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                string[] p = line.Split(' ');
                probes.Add(new XZ(FromBits(p[0]), FromBits(p[1])));
                want.Add(long.Parse(p[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
            var at = new GroundSample[probes.Count];
            world.SampleGround(probes.ToArray(), at);
            var probeHash = new Hasher();
            int differ = 0;
            for (int i = 0; i < at.Length; i++)
            {
                probeHash.Add(at[i]);
                differ += BitConverter.DoubleToInt64Bits(at[i].Height) == want[i] ? 0 : 1;
            }
            cases.Add(("farmland.probes", Hex(probeHash.Value), differ == 0 && at.Length == 1000));
            Console.WriteLine($"  farmland.probes: {at.Length - differ} of {at.Length} heights are validate.py's to the bit");

            var stepHash = new Hasher();
            foreach (TerrainStep s in world.TerrainSteps)
            {
                stepHash.Add(s.AlongZ ? 1L : 0L);
                stepHash.Add(s.C); stepHash.Add(s.From); stepHash.Add(s.To); stepHash.Add(s.Least); stepHash.Add(s.Most);
                foreach (char c in s.Rings)
                    stepHash.Add((long)c);
                Console.WriteLine($"  farmland.steps: {(s.AlongZ ? "x" : "z")} = {s.C}, {s.From} to {s.To} m, {s.Rings}, its face {s.Least:F4} to {s.Most:F4} m");
            }
            cases.Add(("farmland.steps", Hex(stepHash.Value), world.TerrainSteps.Length == 12));

            Patch[] patches = FarmlandPatches();
            var s2 = new Stream(450);
            var points = new XZ[GoldenCount];
            var previous = new Capsule[GoldenCount];
            var current = new Capsule[GoldenCount];
            var rays = new Ray[GoldenCount];
            for (int i = 0; i < GoldenCount; i++)
            {
                Patch patch = patches[i % patches.Length];
                Double3 p = patch.Draw(world, ref s2);
                points[i] = new XZ(p.X, p.Z);
                double r = s2.In(0.003, 0.06);
                Double3 a = patch.Draw(world, ref s2), b = i % 4 == 0 ? a : a + s2.Move(0.15), move = s2.Move(0.3);
                previous[i] = new Capsule(a, b, r);
                current[i] = new Capsule(a + move, b + move + s2.Move(0.02), r);
                Double3 d = s2.Move(1);
                rays[i] = new Ray(patch.Draw(world, ref s2), d.Length() < 1e-3 ? new Double3(0, -1, 0) : d);
            }

            var batch = new GroundSample[points.Length];
            var one = new GroundSample[1];
            world.SampleGround(points, batch);
            Hasher batched = new Hasher(), oneByOne = new Hasher();
            int holes = 0;
            for (int i = 0; i < points.Length; i++)
            {
                batched.Add(batch[i]);
                world.SampleGround(points.AsSpan(i, 1), one);
                oneByOne.Add(one[0]);
                holes += batch[i].Flags == GroundFlags.Hole ? 1 : 0;
            }
            cases.Add(("farmland.ground", Hex(batched.Value), batched.Value == oneByOne.Value));
            Console.WriteLine($"  farmland.ground: {points.Length} samples over {patches.Length} spots, {holes} off the world");

            var contacts = new StaticContact[8];
            var contactHash = new Hasher();
            int touched = 0, early = 0, walls = 0, objects = 0;
            for (int i = 0; i < previous.Length; i++)
            {
                int n = world.StaticContacts(previous[i], current[i], PhysicsMargin, contacts);
                contactHash.Add(n);
                for (int k = 0; k < Math.Min(n, contacts.Length); k++)
                {
                    contactHash.Add(contacts[k]);
                    early += contacts[k].Time < 1 ? 1 : 0;
                    walls += contacts[k].Object == WorldIds.Ground && contacts[k].Normal.Y == 0 ? 1 : 0;
                    objects += contacts[k].Object >= 0 ? 1 : 0;
                }
                touched += n > 0 ? 1 : 0;
            }
            cases.Add(("farmland.contacts", Hex(contactHash.Value), true));
            Console.WriteLine($"  farmland.contacts: {touched} of {previous.Length} capsules touch the ground or an object ({objects} object contacts), {early} with Time < 1, {walls} on a step's face");

            var hits = new RayHit[rays.Length];
            var hit = new RayHit[1];
            world.Raycast(rays, 5, hits);
            Hasher rayBatched = new Hasher(), rayOne = new Hasher();
            int ground = 0, faces = 0;
            for (int i = 0; i < rays.Length; i++)
            {
                rayBatched.Add(hits[i]);
                world.Raycast(rays.AsSpan(i, 1), 5, hit);
                rayOne.Add(hit[0]);
                ground += hits[i].Distance < double.PositiveInfinity && hits[i].Object == WorldIds.Ground ? 1 : 0;
                faces += hits[i].Distance < double.PositiveInfinity && hits[i].Object == WorldIds.Ground && hits[i].Normal.Y == 0 ? 1 : 0;
            }
            cases.Add(("farmland.rays", Hex(rayBatched.Value), rayBatched.Value == rayOne.Value));
            Console.WriteLine($"  farmland.rays: {ground} of {rays.Length} rays hit the ground, {faces} a step's face, {rays.Length - ground - Miss(hits)} an object first");
            cases.AddRange(FarmlandObjectCases(world));
            cases.AddRange(FarmlandStemCases(world));
            return cases;
        }

        /// <summary>The objects' cases (1.2): every placed shape and wind volume; capsules, rays and snag spheres among them.</summary>
        private static List<(string Name, string Value, bool Agree)> FarmlandObjectCases(MapWorld world)
        {
            var cases = new List<(string, string, bool)>();
            var shapeHash = new Hasher();
            int shapes = 0;
            for (int o = 0; o < world.PlacedObjects; o++)
            {
                foreach (char c in world.ObjectName(o))
                    shapeHash.Add((long)c);
                for (int k = 0; world.Geometry(o, k, out ShapeGeometry g); k++, shapes++)
                {
                    shapeHash.Add((long)g.Kind); shapeHash.Add(g.Center); shapeHash.Add(g.AxisX); shapeHash.Add(g.AxisY); shapeHash.Add(g.AxisZ);
                    shapeHash.Add(g.HalfExtents); shapeHash.Add(g.Radius); shapeHash.Add(g.HalfLength); shapeHash.Add(g.Material); shapeHash.Add(g.Surface);
                }
            }
            cases.Add(("farmland.objects", Hex(shapeHash.Value), true));
            Console.WriteLine($"  farmland.objects: {world.PlacedObjects} placed, {shapes} shapes");

            var windHash = new Hasher();
            ReadOnlySpan<WindVolume> volumes = world.WindVolumes;
            for (int k = 0; k < volumes.Length; k++)
            {
                WindVolume w = volumes[k];
                windHash.Add(world.WindVolumeObject(k)); windHash.Add((long)w.Kind); windHash.Add(w.X); windHash.Add(w.Z); windHash.Add(w.BaseM);
                windHash.Add(w.TopM); windHash.Add(w.HalfX); windHash.Add(w.HalfZ); windHash.Add(w.Radius); windHash.Add(w.Yaw); windHash.Add(w.Porosity);
            }
            cases.Add(("farmland.wind", Hex(windHash.Value), true));
            Console.WriteLine($"  farmland.wind: {volumes.Length} wind volumes");

            int[][] spots = SpotObjects(world);
            var s = new Stream(1200);
            var previous = new Capsule[GoldenCount];
            var current = new Capsule[GoldenCount];
            var rays = new Ray[GoldenCount];
            var centres = new Double3[GoldenCount];
            var radii = new double[GoldenCount];
            for (int i = 0; i < GoldenCount; i++)
            {
                int[] objects = spots[i % spots.Length];
                double r = s.In(0.003, 0.06);
                Double3 a = NearShape(world, objects, ref s, 0.6), b = i % 4 == 0 ? a : a + s.Move(0.15), move = s.Move(0.3);
                previous[i] = new Capsule(a, b, r);
                current[i] = new Capsule(a + move, b + move + s.Move(0.02), r);
                Double3 d = s.Move(1);
                rays[i] = new Ray(NearShape(world, objects, ref s, 1.5), d.Length() < 1e-3 ? new Double3(0, -1, 0) : d);
                centres[i] = NearShape(world, objects, ref s, 1.0);
                radii[i] = s.In(0.05, 2);
            }

            var contacts = new StaticContact[64];
            var contactHash = new Hasher();
            int touched = 0, early = 0, objectContacts = 0, overflow = 0;
            for (int i = 0; i < previous.Length; i++)
            {
                int n = world.StaticContacts(previous[i], current[i], PhysicsMargin, contacts);
                contactHash.Add(n);
                bool any = false;
                for (int k = 0; k < Math.Min(n, contacts.Length); k++)
                {
                    contactHash.Add(contacts[k]);
                    early += contacts[k].Time < 1 ? 1 : 0;
                    objectContacts += contacts[k].Object >= 0 ? 1 : 0;
                    any |= contacts[k].Object >= 0;
                }
                touched += any ? 1 : 0;
                overflow += n > contacts.Length ? 1 : 0;
            }
            cases.Add(("farmland.object_contacts", Hex(contactHash.Value), true));
            Console.WriteLine($"  farmland.object_contacts: {touched} of {previous.Length} capsules meet an object, {objectContacts} object contacts, {early} contacts with Time < 1, {overflow} over 64");

            var hits = new RayHit[rays.Length];
            var hit = new RayHit[1];
            world.Raycast(rays, 5, hits);
            Hasher rayBatched = new Hasher(), rayOne = new Hasher();
            int objectHits = 0, groundHits = 0;
            for (int i = 0; i < rays.Length; i++)
            {
                rayBatched.Add(hits[i]);
                world.Raycast(rays.AsSpan(i, 1), 5, hit);
                rayOne.Add(hit[0]);
                objectHits += hits[i].Distance < double.PositiveInfinity && hits[i].Object >= 0 ? 1 : 0;
                groundHits += hits[i].Distance < double.PositiveInfinity && hits[i].Object == WorldIds.Ground ? 1 : 0;
            }
            cases.Add(("farmland.object_rays", Hex(rayBatched.Value), rayBatched.Value == rayOne.Value));
            Console.WriteLine($"  farmland.object_rays: {objectHits} of {rays.Length} rays hit an object, {groundHits} the ground");

            var snags = new SnagSegment[256];
            var snagHash = new Hasher();
            long segments = 0;
            int most = 0, full = 0;
            for (int i = 0; i < centres.Length; i++)
            {
                int n = world.SnagsNear(centres[i], radii[i], snags);
                snagHash.Add(n);
                for (int k = 0; k < Math.Min(n, snags.Length); k++)
                    snagHash.Add(snags[k]);
                segments += n;
                most = Math.Max(most, n);
                full += n > snags.Length ? 1 : 0;
            }
            cases.Add(("farmland.snags", Hex(snagHash.Value), true));
            Console.WriteLine($"  farmland.snags: {centres.Length} spheres of 0.05-2 m, {segments} segments, at most {most} in one, {full} over 256");
            return cases;
        }

        private static int Miss(RayHit[] hits)
        {
            int n = 0;
            foreach (RayHit h in hits)
                n += h.Distance < double.PositiveInfinity ? 0 : 1;
            return n;
        }

        private static double FromBits(string hex) => BitConverter.Int64BitsToDouble(long.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

        // ---- Bench ---------------------------------------------------------------------------------------------------

        /// <summary>A path's points from step −1000 to `steps` + 1, precomputed so the timed steps read them only.</summary>
        private static Func<int, Double3> Precomputed(int steps, Func<int, Double3> path)
        {
            var points = new Double3[steps + 1002];
            for (int s = -1000; s <= steps + 1; s++)
                points[s + 1000] = path(s);
            return s => points[Math.Max(-1000, s) + 1000];
        }

        private static int FarmlandBench()
        {
            Console.WriteLine("  the farmland (w3-farmland-ground 1.1, 1.2), its terrain and its trees, bushes, ruins and debris:");
            var load = Stopwatch.StartNew();
            MapWorld world = Farmland();
            long loaded = GC.GetTotalMemory(true);
            Console.WriteLine($"  farmland.load: {load.ElapsedMilliseconds} ms (17 tiles read and checked against their SHA-256, the steps derived; "
                + $"{world.PlacedObjects} objects placed); the heap after it {loaded / 1048576.0:F0} MiB");
            const int steps = 20000;
            double us = 1e6 / Stopwatch.Frequency;

            // The steepest plot: the village's steepest facet's centroid (cells of 1000/1024 m, both facets of each).
            const double cell = 1000.0 / 1024;
            double steepest = 1, sx = 0, sz = 0;
            var probe = new GroundSample[1];
            for (int cz = 600; cz < 650; cz++)
                for (int cx = 500; cx < 570; cx++)
                    foreach ((double u, double v) in new[] { (2.0 / 3, 1.0 / 3), (1.0 / 3, 2.0 / 3) })
                    {
                        world.SampleGround(new[] { new XZ((cx + u) * cell, (cz + v) * cell) }, probe);
                        if (probe[0].Normal.Y < steepest)
                        {
                            steepest = probe[0].Normal.Y;
                            sx = (cx + u) * cell;
                            sz = (cz + v) * cell;
                        }
                    }
            // Landed on it: the drone turned so its up is the facet's normal, its skids' undersides 1 mm into the plane, trembling
            // 1 um along it. Physics' pattern turned with it; K§10.5's batch (level) skips this row.
            world.SampleGround(new[] { new XZ(sx, sz) }, probe);
            Double3 up = probe[0].Normal, plot = new Double3(sx, probe[0].Height, sz) + up * 0.252;
            Double3 Turn(Double3 v) // Rodrigues: (0, 1, 0) onto up
            {
                Double3 k = new Double3(up.Z, 0, -up.X);
                double sin = k.Length(), cos = up.Y;
                if (!(sin > 0))
                    return v;
                k = k * (1 / sin);
                Double3 kv = new Double3(k.Y * v.Z - k.Z * v.Y, k.Z * v.X - k.X * v.Z, k.X * v.Y - k.Y * v.X);
                return v * cos + kv * sin + k * (Double3.Dot(k, v) * (1 - cos));
            }
            Double3 Over(double x, double z, double height) => new Double3(x, GroundAt(world, x, z) + height, z);
            string landed = $"landed on the village's steepest plot ({Math.Acos(up.Y) * 180 / Math.PI:F1} deg), tilted onto it, the skids 1 mm in, trembling 1 um";
            (string Name, Func<int, Double3> Path)[] paths =
            {
                ("low over the village's plots, 0.3-1.5 m up", Precomputed(steps, s => Over(495 + 65 * Tri(s, 2 * 487), 600 + 40 * Tri(s, 2 * 593), 0.3 + 1.2 * Tri(s, 2 * 709)))),
                (landed, Precomputed(steps, s => plot + up * (1e-6 * Tri(s, 14)))),
                ("skimming a field at 0.3 m, 15 m/s", Precomputed(steps, s => Over(150 + 0.015 * Math.Max(0, s), 230, 0.3))),
                ("along the ledge's face 0.3 m out over the far ring at mid-face, 10 m/s",
                    Precomputed(steps, s => Over(2000.0001, 0.01 * Math.Max(0, s), 0.75) + new Double3(0.3 - 0.0001, 0, 0))),
                ("over the far ring at 1-3 m (its 11.8 m facets)", Precomputed(steps, s => Over(-3000 + 100 * Tri(s, 2 * 487), 3000 + 100 * Tri(s, 2 * 593), 1 + 2 * Tri(s, 2 * 709)))),
                // The objects (1.2): through the village's ruins and debris, the F6|F7 belt's crowns and a bush cluster.
                ("through the village's ruins and debris, 0.3-2.5 m up, 10-20 m/s", Precomputed(steps, s => Over(468 + 80 * Tri(s, 2 * 4111), 576 + 52 * Tri(s, 2 * 3313), 0.3 + 2.2 * Tri(s, 2 * 709)))),
                ("through the F6|F7 belt's crowns and bushes, 1-9 m up, 10-20 m/s", Precomputed(steps, s => Over(298 + 98 * Tri(s, 2 * 4877), 753 + 14 * Tri(s, 2 * 1013), 1 + 8 * Tri(s, 2 * 1531)))),
                ("through a bush cluster, 0.2-1.5 m up, 10-20 m/s", Precomputed(steps, s => Over(102 + 46 * Tri(s, 2 * 2311), 252 + 46 * Tri(s, 2 * 2707), 0.2 + 1.3 * Tri(s, 2 * 613)))),
            };
            var times = new long[steps];
            int failures = 0;
            Pattern physics = PhysicsPattern(), tilted = PhysicsPattern(Turn);
            physics.Reset();
            tilted.Reset();
            foreach (Pattern level in new[] { physics, BatchPattern() })
            {
                foreach (var path in paths)
                {
                    if (path.Name == landed && level != physics)
                        continue;
                    Pattern pattern = path.Name == landed ? tilted : level;
                    long queries = 0, allocated = 0;
                    for (int pass = 0; pass < 2; pass++) // the first pass warms the JIT and the caches
                    {
                        long before = Fpv450.Sim.Tests.AllocProbe.Before();
                        queries = 0;
                        for (int s = 0; s < steps; s++)
                        {
                            long start = Stopwatch.GetTimestamp();
                            pattern.Step(world, path.Path, s);
                            times[s] = Stopwatch.GetTimestamp() - start;
                            queries += pattern.Queries;
                        }
                        allocated = Fpv450.Sim.Tests.AllocProbe.After(before);
                    }
                    Array.Sort(times);
                    double median = times[steps / 2] * us, p99 = times[steps * 99 / 100] * us, most = times[steps - 1] * us;
                    bool judged = level == physics, over = judged && !(p99 < 300);
                    failures += over || allocated != 0 ? 1 : 0;
                    Console.WriteLine($"  {pattern.Name}, {path.Name}: median {median:F2} us, p99 {p99:F2} us, max {most:F1} us, "
                        + $"{(double)queries / steps:F1} swept queries a step, {allocated} B; the 60 us target {(p99 < 60 ? "met" : "missed")} at p99"
                        + (over ? " (OVER the step's 300 us)" : judged ? "" : " (shown)"));
                }
            }

            double perSweeps = (physics.SweepTicks + tilted.SweepTicks) * 1e3 / Stopwatch.Frequency * 100000 / (physics.Sweeps + tilted.Sweeps);
            double perRays = (physics.RayTicks + tilted.RayTicks) * 1e3 / Stopwatch.Frequency * 100000 / (physics.Rays + tilted.Rays);
            Console.WriteLine($"  farmland.capsules in physics' pattern ({physics.Sweeps + tilted.Sweeps} swept over the five paths): {perSweeps:F2} ms per 100000 (budget 100 ms)");
            Console.WriteLine($"  farmland.rays in physics' pattern ({physics.Rays + tilted.Rays} rotor rays of 5 m): {perRays:F2} ms per 100000 (budget 100 ms)");
            failures += perSweeps < 100 && perRays < 100 ? 0 : 1;

            // 100 000 samples over the golden's spots, and capsules and rays there (shown).
            Patch[] patches = FarmlandPatches();
            const int n = 100000;
            var stream = new Stream(7);
            var points = new XZ[n];
            var previous = new Capsule[n];
            var current = new Capsule[n];
            var rays = new Ray[n];
            for (int i = 0; i < n; i++)
            {
                Patch patch = patches[i % patches.Length];
                Double3 p = patch.Draw(world, ref stream), a = patch.Draw(world, ref stream);
                points[i] = new XZ(p.X, p.Z);
                Double3 b = i % 4 == 0 ? a : a + stream.Move(0.15), move = stream.Move(0.03);
                previous[i] = new Capsule(a, b, 0.01);
                current[i] = new Capsule(a + move, b + move, 0.01);
                Double3 d = stream.Move(1);
                rays[i] = new Ray(patch.Draw(world, ref stream), d.Length() < 1e-3 ? new Double3(0, -1, 0) : d);
            }
            var samples = new GroundSample[n];
            var hits = new RayHit[n];
            var contacts = new StaticContact[16];
            world.SampleGround(points, samples);
            for (int i = 0; i < 1000; i++)
                world.StaticContacts(previous[i], current[i], PhysicsMargin, contacts);
            world.Raycast(rays.AsSpan(0, 1000), 2, hits);
            long a0 = Fpv450.Sim.Tests.AllocProbe.Before(), t0 = Stopwatch.GetTimestamp();
            world.SampleGround(points, samples);
            long t1 = Stopwatch.GetTimestamp();
            for (int i = 0; i < n; i++)
                world.StaticContacts(previous[i], current[i], PhysicsMargin, contacts);
            long t2 = Stopwatch.GetTimestamp();
            world.Raycast(rays, 2, hits);
            long t3 = Stopwatch.GetTimestamp(), a1 = a0 + Fpv450.Sim.Tests.AllocProbe.After(a0);
            double ms = 1e3 / Stopwatch.Frequency;
            Console.WriteLine($"  farmland.samples {n} over the {patches.Length} spots: {(t1 - t0) * ms:F2} ms (budget 10 ms)");
            Console.WriteLine($"  farmland.capsules {n} at random poses there, moving up to 30 mm: {(t2 - t1) * ms:F2} ms (shown)");
            Console.WriteLine($"  farmland.rays {n} of <= 2 m at random poses there: {(t3 - t2) * ms:F2} ms (shown); {a1 - a0} B allocated by all three");
            failures += (t1 - t0) * ms < 10 && a1 == a0 ? 0 : 1;

            // A laid fiber segment resting on the ground (both ends 0.1 mm into it, the line's 0.125 mm radius, the clearance
            // margin 0.5 m): one still query's time by its length, the ground left out as the fiber's own queries leave it
            // (QueryFilter.NoGround, 2.1), so the time is the objects' near it. Shown.
            foreach (double length in new[] { 0.5, 2, 10, 50, 200 })
            {
                const int laid = 200;
                var cap = new Capsule[laid];
                for (int i = 0; i < laid; i++)
                {
                    double x = 100 + 3.7 * i, z = 100 + 1.3 * i, ex = x + 0.6 * length, ez = z + 0.8 * length;
                    cap[i] = new Capsule(Over(x, z, 0.000125 - 0.0001), Over(ex, ez, 0.000125 - 0.0001), 0.000125);
                }
                for (int i = 0; i < laid; i++)
                    world.StaticContacts(cap[i], cap[i], 0.5, contacts, QueryFilter.NoGround);
                var each = new long[laid];
                long near = 0;
                for (int i = 0; i < laid; i++)
                {
                    long start = Stopwatch.GetTimestamp();
                    int found = world.StaticContacts(cap[i], cap[i], 0.5, contacts, QueryFilter.NoGround);
                    each[i] = Stopwatch.GetTimestamp() - start;
                    near += found;
                }
                Array.Sort(each);
                Console.WriteLine($"  farmland.fiber: a laid segment of {length} m on the ground, margin 0.5 m, NoGround: median {each[laid / 2] * us:F2} us, "
                    + $"max {each[laid - 1] * us:F1} us a query, {(double)near / laid:F2} objects' shapes within the margin (shown)");
            }

            // SnagsNear at the fiber caches' radii (tether.json: the drone's 2 m, a line node's 1 m), centred on the objects'
            // shapes in the village, the belt and a bush cluster. It runs at the caches' refresh, off the step: shown.
            int[][] spots = SpotObjects(world);
            var snags = new SnagSegment[1 << 14];
            foreach (double radius in new[] { 2.0, 1.0 })
            {
                foreach (int spot in new[] { 0, 1, 3 })
                {
                    const int asked = 2000;
                    var stream2 = new Stream(31 + (ulong)spot);
                    var centres = new Double3[asked];
                    for (int i = 0; i < asked; i++)
                        centres[i] = NearShape(world, spots[spot], ref stream2, 1.0);
                    for (int i = 0; i < asked; i++)
                        world.SnagsNear(centres[i], radius, snags);
                    var each = new long[asked];
                    long found = 0, before = Fpv450.Sim.Tests.AllocProbe.Before();
                    int most = 0;
                    for (int i = 0; i < asked; i++)
                    {
                        long start = Stopwatch.GetTimestamp();
                        int got = world.SnagsNear(centres[i], radius, snags);
                        each[i] = Stopwatch.GetTimestamp() - start;
                        found += got;
                        most = Math.Max(most, got);
                    }
                    long bytes = Fpv450.Sim.Tests.AllocProbe.After(before);
                    Array.Sort(each);
                    failures += bytes == 0 ? 0 : 1;
                    Console.WriteLine($"  farmland.snags r {radius} m, {ObjectSpots[spot].Name}: median {each[asked / 2] * us:F1} us, p99 {each[asked * 99 / 100] * us:F1} us, "
                        + $"max {each[asked - 1] * us:F1} us; {(double)found / asked:F0} segments a query, at most {most}; {bytes} B (shown: the caches' refresh, off the step)");
                }
            }

            failures += FarmlandStemBench(world);
            Console.WriteLine(failures == 0 ? "worldbench: farmland bench done, every budget met (judged on AC only)" : $"worldbench: farmland {failures} over budget or allocating");
            return failures == 0 ? 0 : 1;
        }
    }
}
