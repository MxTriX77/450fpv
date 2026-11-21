using System;
using System.Collections.Generic;
using System.Threading;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7d-tether 1.3: SnagsNear on the test range (the world-query spec's "Records near a point"). The expected segments are
    // brute force over every placed snag record, read independently (TestJson) and placed by Math.Sin and Cos of the row's
    // yaw. A segment whose distance lies within Ambiguous of the sphere's reach is left out of the comparison (printed).
    // The 1 m query's time is judged by tools/worldbench --bench (SnagsBench), on AC; the 2048-segment cap per 2 m cube is
    // the map compiler's (tools/map/tests).
    public sealed class SnagsNearTests
    {
        private const double Ambiguous = 1e-9; // two parsers and two rotations: a few ulps of a few hundred metres
        private const double Exact = 1e-9;

        /// <summary>One record segment as the files give it, placed independently.</summary>
        private sealed class Expected
        {
            public int Object, Record, Segment;
            public SnagKind Kind;
            public double Radius, Edge, Pitch;
            public string Material;
            public Double3 A, B;
        }

        private static List<Expected> _all;

        private static Dictionary<string, object> Obj(object n) => (Dictionary<string, object>)n;

        private static List<object> List(object n) => (List<object>)n;

        private static Double3 Vec(object n) => new Double3((double)List(n)[0], (double)List(n)[1], (double)List(n)[2]);

        /// <summary>Every placed record segment, in canonical order.</summary>
        private static List<Expected> All => _all ?? (_all = ReadAll());

        private static List<Expected> ReadAll()
        {
            var all = new List<Expected>();
            var metas = new Dictionary<string, Dictionary<string, object>>();
            foreach (object r in List(Obj(TestJson.Parse(MapWorldTests.Placements()))["instances"]))
            {
                var row = Obj(r);
                string path = (string)row["class"] + "/" + (string)row["asset"] + "/" + (string)row["variant"] + ".assetmeta.json";
                if (!metas.TryGetValue(path, out Dictionary<string, object> meta))
                    metas.Add(path, meta = Obj(TestJson.Parse(MapWorldTests.ReadModel(path))));
                if (!meta.TryGetValue("snag", out object snags))
                    continue;
                Double3 at = Vec(row["position_m"]);
                double yaw = (double)row["yaw_deg"] * Math.PI / 180, c = Math.Cos(yaw), s = Math.Sin(yaw);
                Double3 Place(Double3 p) => new Double3(at.X + p.X * c + p.Z * s, at.Y + p.Y, at.Z - p.X * s + p.Z * c); // Unity's yaw
                List<object> records = List(snags);
                for (int k = 0; k < records.Count; k++)
                {
                    var record = Obj(records[k]);
                    List<object> points = List(record["points"]);
                    double radius = (double)record["radius_m"];
                    for (int j = 0; j + 1 < points.Count; j++)
                    {
                        all.Add(new Expected
                        {
                            Object = (int)(double)row["id"], Record = k, Segment = j,
                            Kind = (string)record["kind"] == "edge" ? SnagKind.Edge : SnagKind.Hook,
                            Radius = radius,
                            Edge = record.TryGetValue("edge_radius_m", out object e) ? (double)e : radius,
                            Pitch = record.TryGetValue("barb_pitch_m", out object p) ? (double)p : 0,
                            Material = (string)record["material"],
                            A = Place(Vec(points[j])), B = Place(Vec(points[j + 1])),
                        });
                    }
                }
            }
            return all;
        }

        private static double Distance(Double3 c, Double3 a, Double3 b)
        {
            Double3 ab = b - a, ac = c - a;
            double dd = Double3.Dot(ab, ab), t = dd > 0 ? Math.Max(0, Math.Min(1, Double3.Dot(ac, ab) / dd)) : 0;
            Double3 e = ac - ab * t;
            return Math.Sqrt(Double3.Dot(e, e));
        }

        private static bool Before(in SnagSegment a, in SnagSegment b) =>
            a.Object < b.Object || a.Object == b.Object && (a.Record < b.Record || a.Record == b.Record && a.Segment < b.Segment);

        private static void Near(double got, double want, string what) =>
            Assert.That(got, Is.EqualTo(want).Within(1e-12 * Math.Max(1, Math.Abs(want))), what);

        /// <summary>The world's answer against brute force: the same set in canonical order, each field the record's.</summary>
        private static SnagSegment[] AssertBruteForce(IWorldQuery world, Double3 c, double r, string what, out int n)
        {
            var got = new SnagSegment[8192];
            n = world.SnagsNear(c, r, got);
            Assert.That(n, Is.LessThanOrEqualTo(got.Length), what + ": buffer");
            var mustHave = new List<Expected>();
            var mayHave = new HashSet<(int, int, int)>();
            foreach (Expected e in All)
            {
                double gap = Distance(c, e.A, e.B) - (r + e.Radius);
                if (gap < -Ambiguous)
                    mustHave.Add(e);
                else if (gap <= Ambiguous)
                    mayHave.Add((e.Object, e.Record, e.Segment));
            }
            var objects = new HashSet<int>();
            foreach (Expected e in mustHave)
                objects.Add(e.Object);
            TestContext.Out.WriteLine($"{what}: {n} segments of {objects.Count} objects (brute force {mustHave.Count}, {mayHave.Count} on the reach within {Ambiguous} m)");
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                if (k > 0)
                    Assert.That(Before(got[k - 1], got[k]), Is.True, what + ": canonical order");
                if (m < mustHave.Count && (got[k].Object, got[k].Record, got[k].Segment) == (mustHave[m].Object, mustHave[m].Record, mustHave[m].Segment))
                {
                    Expected e = mustHave[m++];
                    string at = $"{what}: object {e.Object} record {e.Record} segment {e.Segment}";
                    Assert.That(got[k].Kind, Is.EqualTo(e.Kind), at + " kind");
                    Near(got[k].Radius, e.Radius, at + " radius_m");
                    Near(got[k].EdgeRadius, e.Edge, at + " edge_radius_m (else radius_m)");
                    Near(got[k].BarbPitch, e.Pitch, at + " barb_pitch_m (else 0)");
                    Assert.That(world.MaterialName(got[k].Material), Is.EqualTo(e.Material), at + " material");
                    Assert.That((got[k].A - e.A).Length(), Is.LessThan(Exact), at + " A");
                    Assert.That((got[k].B - e.B).Length(), Is.LessThan(Exact), at + " B");
                    continue;
                }
                Assert.That(mayHave.Contains((got[k].Object, got[k].Record, got[k].Segment)), Is.True,
                    $"{what}: object {got[k].Object} record {got[k].Record} segment {got[k].Segment} is not within the sphere");
            }
            Assert.That(m, Is.EqualTo(mustHave.Count), what + ": every segment meeting the sphere within its radius_m is returned");
            return got;
        }

        private static int Find(string variant, int occurrence = 0)
        {
            for (int obj = 0; obj < MapWorldTests.World.PlacedObjects; obj++)
                if (MapWorldTests.World.ObjectName(obj) == variant && occurrence-- == 0)
                    return obj;
            Assert.Fail(variant + " is not placed");
            return -1;
        }

        /// <summary>The mean of an object's expected segment ends: inside a crown, the crown's middle.</summary>
        private static Double3 Middle(int obj)
        {
            Double3 sum = default;
            int n = 0;
            foreach (Expected e in All)
                if (e.Object == obj)
                {
                    sum = sum + e.A + e.B;
                    n += 2;
                }
            return sum * (1.0 / n);
        }

        /// <summary>
        /// The spec's points: an L01 pole's head (its row's position, 6.6 m up), along an L03 span (three points of its
        /// chain) and in V01_L's crown (the first point of an edge and of a hook record there).
        /// </summary>
        private static (string Name, Double3 At)[] Points()
        {
            MapWorld world = MapWorldTests.World;
            int pole = Find("L01_straight"), span = Find("L03_medium", 1), crown = Find("V01_L");
            var row = Obj(List(Obj(TestJson.Parse(MapWorldTests.Placements()))["instances"])[pole]);
            Double3 head = Vec(row["position_m"]) + new Double3(0, 6.6, 0);
            ReadOnlySpan<Double3> chain = world.WirePoints(span, 0);
            Expected edge = All.Find(e => e.Object == crown && e.Kind == SnagKind.Edge && e.Record > 20 && e.Segment == 0);
            Expected hook = All.Find(e => e.Object == crown && e.Kind == SnagKind.Hook && e.Record > 20 && e.Segment == 0);
            return new[]
            {
                ($"L01_straight's head (object {pole})", head),
                ($"L03_medium's chain point 10 (object {span})", chain[10]),
                ($"L03_medium's chain point 40 (object {span})", chain[40]),
                ($"L03_medium's chain point 75 (object {span})", chain[75]),
                ($"V01_L's crown at edge record {edge.Record} (object {crown})", edge.A),
                ($"V01_L's crown at hook record {hook.Record} (object {crown})", hook.A),
            };
        }

        // ---- Records near a point -------------------------------------------------------------------------------------

        [Test]
        public void RecordsNearAPoint_AreTheBruteForceSetInCanonicalOrder_WithEveryFieldOfTheirRecord()
        {
            MapWorld world = MapWorldTests.World;
            int returned = 0;
            var kinds = new HashSet<SnagKind>();
            var edges = 0;
            foreach ((string name, Double3 at) in Points())
            {
                foreach (double r in new[] { 1.0, 2.0 })
                {
                    SnagSegment[] got = AssertBruteForce(world, at, r, $"{name}, r {r}", out int n);
                    Assert.That(n, Is.GreaterThan(0), name + ": a record lies there");
                    returned += n;
                    for (int k = 0; k < n; k++)
                    {
                        kinds.Add(got[k].Kind);
                        edges += got[k].EdgeRadius < got[k].Radius ? 1 : 0;
                    }

                    // A buffer too small holds the canonical prefix, and the true count comes back.
                    var small = new SnagSegment[3];
                    Assert.That(world.SnagsNear(at, r, small), Is.EqualTo(n), name + ": the true count");
                    for (int k = 0; k < Math.Min(n, small.Length); k++)
                        Assert.That((small[k].Object, small[k].Record, small[k].Segment, small[k].A.Y), Is.EqualTo((got[k].Object, got[k].Record, got[k].Segment, got[k].A.Y)),
                            $"{name}, r {r}: prefix {k}");
                    Assert.That(world.SnagsNear(at, r, Span<SnagSegment>.Empty), Is.EqualTo(n));
                }
            }
            TestContext.Out.WriteLine($"{returned} segments returned; {edges} with an edge radius under their reach");
            Assert.That(kinds, Is.EquivalentTo(new[] { SnagKind.Edge, SnagKind.Hook }), "both kinds met");
            Assert.That(edges, Is.GreaterThan(0), "the span's strands carry their edge radius");
            Assert.That(AssertBruteForceCount(world, new Double3(40, 1, 45), 2), Is.EqualTo(0), "the open field: none");
            Assert.That(new FloorWorld(0, "meadow_sod", "steel").SnagsNear(default, 30, new SnagSegment[4]), Is.EqualTo(0), "the floor stub has none");
        }

        private static int AssertBruteForceCount(IWorldQuery world, Double3 c, double r)
        {
            AssertBruteForce(world, c, r, $"at ({c.X}, {c.Y}, {c.Z}), r {r}", out int n);
            return n;
        }

        [Test]
        public void SpheresOverTheRange_AreTheBruteForceSet()
        {
            // 60 spheres of 0.05-6 m round the line, the crowns, the houses and the cars.
            MapWorld world = MapWorldTests.World;
            var rng = new Random(13);
            int checkedSpheres = 0;
            foreach (string variant in new[] { "L01_braced", "L03_medium", "V01_L", "V01_M", "V13_tall", "H01_D", "H01_I", "R01_A", "R01_F" })
            {
                Double3 middle = Middle(Find(variant));
                for (int i = 0; i < 7; i++, checkedSpheres++)
                {
                    Double3 c = middle + new Double3(8 * rng.NextDouble() - 4, 4 * rng.NextDouble() - 2, 8 * rng.NextDouble() - 4);
                    AssertBruteForce(world, c, 0.05 + 5.95 * rng.NextDouble(), $"{variant} {i}", out _);
                }
            }
            Assert.That(checkedSpheres, Is.EqualTo(63));
        }

        [Test]
        public void ABarbPitchAndAnEdgeRadius_AreCarried_AndBadRecordsAreRefusedNamingTheField()
        {
            // The fortifications reading §1's fields on L03_medium's four strand records (a synthetic edit: no placed asset
            // carries a barb pitch yet).
            string placements = MapWorldTests.Placements();
            const string edge = "\"edge_radius_m\": 0.0015,";
            StringAssert.Contains(edge, MapWorldTests.ReadModel("lines/L03/L03_medium.assetmeta.json"));
            Func<string, string> Edit(string file, string from, string to) => path =>
                path.EndsWith(file, StringComparison.Ordinal) ? MapWorldTests.ReadModel(path).Replace(from, to) : MapWorldTests.ReadModel(path);
            var world = new MapWorld(placements, Edit("L03_medium.assetmeta.json", edge, edge + " \"barb_pitch_m\": 0.035,"), MapWorldTests.Materials());
            int span = Find("L03_medium", 1);
            Double3 at = world.WirePoints(span, 0)[40];
            var got = new SnagSegment[64];
            int n = world.SnagsNear(at, 0.5, got);
            int strands = 0;
            for (int k = 0; k < n; k++)
            {
                if (got[k].Object != span)
                    continue;
                Assert.That((got[k].Kind, got[k].Radius, got[k].EdgeRadius, got[k].BarbPitch, world.MaterialName(got[k].Material)),
                    Is.EqualTo((SnagKind.Edge, 0.0045, 0.0015, 0.035, "cable")), "record " + got[k].Record);
                strands++;
            }
            Assert.That(strands, Is.GreaterThanOrEqualTo(2), "the strand's segments either side of its chain point 40");
            var conductors = new Conductor[8];
            world.WiresNear(at, 0.5, conductors);
            Assert.That(conductors[0].EdgeRadius, Is.EqualTo(0.0015), "the wire still reads its edge record's edge radius");

            const string hook = "\"radius_m\": 0.0085,";
            StringAssert.Contains(hook, MapWorldTests.ReadModel("lines/L01/L01_straight.assetmeta.json"));
            foreach ((string file, string from, string to, string field) in new[]
            {
                ("L01_straight.assetmeta.json", hook, hook + " \"barb_pitch_m\": 0.035,", "barb_pitch_m"),
                ("L03_medium.assetmeta.json", edge, edge + " \"barb_pitch_m\": 0.0,", "barb_pitch_m"),
                ("L03_medium.assetmeta.json", edge, "\"edge_radius_m\": 0.005,", "edge_radius_m"),
                ("L01_straight.assetmeta.json", hook, "\"radius_m\": 0.0,", "radius_m"),
                ("L01_straight.assetmeta.json", "\"kind\": \"hook\"", "\"kind\": \"barb\"", "kind"),
                ("L01_straight.assetmeta.json", "\"material\": \"steel\"", "\"material\": \"surface:meadow_sod\"", "material"),
            })
            {
                var e = Assert.Throws<FormatException>(() => new MapWorld(placements, Edit(file, from, to), MapWorldTests.Materials()), to);
                StringAssert.Contains(field, e.Message);
                StringAssert.Contains(file, e.Message);
                TestContext.Out.WriteLine(e.Message);
            }
        }

        [Test]
        public void SnagsNear_AllocatesNothing()
        {
            MapWorld world = MapWorldTests.World;
            (string Name, Double3 At)[] points = Points();
            var results = new SnagSegment[4096];
            world.SnagsNear(points[0].At, 2, results);
            long before = AllocProbe.Before();
            for (int i = 0; i < 120; i++)
                world.SnagsNear(points[i % points.Length].At + new Double3(0.05 * i, 0, 0), i % 2 == 0 ? 1 : 2, results);
            // Is.Zero, not Is.EqualTo(0): .NET's JIT boxes that 0 (24 B) before the closing count is read.
            Assert.That(AllocProbe.After(before), Is.Zero, AllocProbe.Report());
        }

        // ---- Determinism ------------------------------------------------------------------------------------------------

        private static ulong RunQueries(IWorldQuery world, (string Name, Double3 At)[] points, int seed, int count)
        {
            var rng = new Random(seed);
            var results = new SnagSegment[1024];
            ulong hash = WorldHash.Offset;
            void Add(ulong v)
            {
                for (int k = 0; k < 8; k++)
                    hash = (hash ^ (byte)(v >> 8 * k)) * 0x100000001B3UL;
            }
            void AddD(double v) => Add((ulong)BitConverter.DoubleToInt64Bits(v));
            for (int i = 0; i < count; i++)
            {
                Double3 c = points[i % points.Length].At + new Double3(4 * rng.NextDouble() - 2, 2 * rng.NextDouble() - 1, 4 * rng.NextDouble() - 2);
                int n = world.SnagsNear(c, 0.05 + 2.95 * rng.NextDouble(), results);
                Add((ulong)n);
                for (int k = 0; k < Math.Min(n, results.Length); k++)
                {
                    Add((ulong)results[k].Object); Add((ulong)results[k].Record); Add((ulong)results[k].Segment);
                    AddD(results[k].EdgeRadius); AddD(results[k].A.X); AddD(results[k].B.Y);
                }
            }
            return hash;
        }

        [Test]
        public void Concurrency_FourThreadsOnOneWorldGetTheBitsOfOne()
        {
            MapWorld world = MapWorldTests.World;
            (string Name, Double3 At)[] points = Points();
            const int count = 2000;
            ulong alone = RunQueries(world, points, 9, count);
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
                            results[me] = RunQueries(world, points, 9, count);
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
    }
}
