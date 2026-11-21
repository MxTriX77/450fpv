using System;
using System.Collections.Generic;
using System.Threading;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7d-tether 1.2: WiresNear and the wire filter on the test range (the world-query spec's "The range's spans and their
    // ends" and "The filter"). The expected conductors are brute force over every placed wire record, read independently
    // (TestJson) and placed by Math.Sin and Cos of the row's yaw, their blow-out by Math.Sin(π/8); the expected ends are the
    // placement file's hang records, the map's own statement of the hooks each span hangs from. A conductor whose distance
    // lies within Ambiguous of the sphere's reach is left out of the comparison (it never happened on these spheres: printed).
    // The 2 m query's time is judged by tools/worldbench --bench (WiresBench), on AC.
    public sealed class WiresNearTests
    {
        private const double Ambiguous = 1e-9; // two parsers and two rotations: a few ulps of a few hundred metres
        private const double Exact = 1e-9;

        /// <summary>One conductor as the files give it, placed independently.</summary>
        private sealed class Expected
        {
            public int Object, Shape, Wire;
            public double Diameter, Mass, Sag, Axial, Breaking, Bending, Edge, Reach;
            public Double3[] World;
        }

        private static List<Expected> _all;
        private static List<object> _rows;

        private static Dictionary<string, object> Obj(object n) => (Dictionary<string, object>)n;

        private static List<object> List(object n) => (List<object>)n;

        private static Double3 Vec(object n) => new Double3((double)List(n)[0], (double)List(n)[1], (double)List(n)[2]);

        private static List<object> Rows => _rows ?? (_rows = List(Obj(TestJson.Parse(MapWorldTests.Placements()))["instances"]));

        private static readonly Dictionary<string, Dictionary<string, object>> Metas = new Dictionary<string, Dictionary<string, object>>();

        private static Dictionary<string, object> Meta(Dictionary<string, object> row)
        {
            string path = (string)row["class"] + "/" + (string)row["asset"] + "/" + (string)row["variant"] + ".assetmeta.json";
            if (!Metas.TryGetValue(path, out Dictionary<string, object> meta))
                Metas.Add(path, meta = Obj(TestJson.Parse(MapWorldTests.ReadModel(path))));
            return meta;
        }

        private static void Near(double got, double want, string what) =>
            Assert.That(got, Is.EqualTo(want).Within(1e-12 * Math.Max(1, Math.Abs(want))), what);

        /// <summary>Every placed wire, in canonical order.</summary>
        private static List<Expected> All => _all ?? (_all = ReadAll());

        private static List<Expected> ReadAll()
        {
            var all = new List<Expected>();
            foreach (object r in Rows)
            {
                var row = Obj(r);
                var meta = Meta(row);
                List<object> wires = List(meta["wires"]);
                Double3 at = Vec(row["position_m"]);
                double yaw = (double)row["yaw_deg"] * Math.PI / 180, c = Math.Cos(yaw), s = Math.Sin(yaw);
                for (int w = 0; w < wires.Count; w++)
                {
                    var wire = Obj(wires[w]);
                    List<object> points = List(wire["points"]);
                    var x = new Expected
                    {
                        Object = (int)(double)row["id"], Shape = List(meta["collision"]).Count + w, Wire = w, Diameter = (double)wire["diameter_m"],
                        Mass = (double)wire["mass_kg_per_m"], Sag = (double)wire["sag_m"], Axial = (double)wire["axial_stiffness_n"],
                        Breaking = (double)wire["breaking_force_n"], Bending = (double)wire["bending_stiffness_n_m2"], World = new Double3[points.Count],
                    };
                    for (int k = 0; k < points.Count; k++)
                    {
                        Double3 p = Vec(points[k]); // Unity's yaw: +Z turns toward +X
                        x.World[k] = new Double3(at.X + p.X * c + p.Z * s, at.Y + p.Y, at.Z - p.X * s + p.Z * c);
                    }
                    // The farthest point from the chord: the blow-out's arm.
                    Double3 a = x.World[0], chord = x.World[points.Count - 1] - a;
                    chord = chord * (1 / Math.Sqrt(Double3.Dot(chord, chord)));
                    double far = 0;
                    foreach (Double3 p in x.World)
                    {
                        Double3 off = p - a - chord * Double3.Dot(p - a, chord);
                        far = Math.Max(far, Math.Sqrt(Double3.Dot(off, off)));
                    }
                    x.Reach = x.Diameter / 2 + 2 * far * Math.Sin(Math.PI / 8);
                    x.Edge = x.Diameter / 2;
                    foreach (object n in List(meta["snag"]))
                    {
                        var snag = Obj(n);
                        if ((string)snag["kind"] == "edge" && SamePoints(List(snag["points"]), points))
                            x.Edge = snag.TryGetValue("edge_radius_m", out object e) ? (double)e : x.Edge;
                    }
                    all.Add(x);
                }
            }
            return all;
        }

        private static bool SamePoints(List<object> a, List<object> b)
        {
            if (a.Count != b.Count)
                return false;
            for (int k = 0; k < a.Count; k++)
                for (int i = 0; i < 3; i++)
                    if ((double)List(a[k])[i] != (double)List(b[k])[i])
                        return false;
            return true;
        }

        private static double Distance(Double3 c, Double3[] line)
        {
            double best = double.PositiveInfinity;
            for (int k = 0; k + 1 < line.Length; k++)
            {
                Double3 a = line[k], ab = line[k + 1] - a, ac = c - a;
                double t = Math.Max(0, Math.Min(1, Double3.Dot(ac, ab) / Double3.Dot(ab, ab)));
                Double3 e = ac - ab * t;
                best = Math.Min(best, Math.Sqrt(Double3.Dot(e, e)));
            }
            return best;
        }

        private static Conductor[] Query(IWorldQuery world, Double3 c, double r, int buffer, out int count)
        {
            var results = new Conductor[buffer];
            count = world.WiresNear(c, r, results);
            return results;
        }

        /// <summary>The world's answer against brute force: the same set in canonical order, each field the record's.</summary>
        private static Conductor[] AssertBruteForce(IWorldQuery world, Double3 c, double r, string what, out int n)
        {
            Conductor[] got = Query(world, c, r, 256, out n);
            Assert.That(n, Is.LessThanOrEqualTo(got.Length), what + ": buffer");
            var mustHave = new List<Expected>();
            var mayHave = new HashSet<(int, int)>();
            foreach (Expected e in All)
            {
                double gap = Distance(c, e.World) - (r + e.Reach);
                if (gap < -Ambiguous)
                    mustHave.Add(e);
                else if (gap <= Ambiguous)
                    mayHave.Add((e.Object, e.Shape));
            }
            TestContext.Out.WriteLine($"{what}: {n} conductors (brute force {mustHave.Count}, {mayHave.Count} on the reach within {Ambiguous} m)");
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                if (k > 0)
                    Assert.That(got[k].Object > got[k - 1].Object || got[k].Object == got[k - 1].Object && got[k].Shape > got[k - 1].Shape, Is.True,
                        what + ": canonical order");
                if (m < mustHave.Count && got[k].Object == mustHave[m].Object && got[k].Shape == mustHave[m].Shape)
                {
                    Expected e = mustHave[m++];
                    string at = $"{what}: object {e.Object} wire {e.Wire}";
                    Near(got[k].Diameter, e.Diameter, at + " diameter");
                    Near(got[k].MassPerMetre, e.Mass, at + " mass per metre");
                    Near(got[k].Sag, e.Sag, at + " sag");
                    Near(got[k].AxialStiffness, e.Axial, at + " EA");
                    Near(got[k].BreakingForce, e.Breaking, at + " breaking force");
                    Near(got[k].BendingStiffness, e.Bending, at + " EI");
                    Near(got[k].EdgeRadius, e.Edge, at + ": its edge snag's edge radius");
                    Assert.That(world.MaterialName(got[k].Material), Is.EqualTo("cable"), at);
                    ReadOnlySpan<Double3> points = got[k].Points.Span;
                    Assert.That(points.Length, Is.EqualTo(e.World.Length), at + ": its chain");
                    for (int j = 0; j < points.Length; j++)
                        Assert.That((points[j] - e.World[j]).Length(), Is.LessThan(Exact), at + " point " + j);
                    continue;
                }
                Assert.That(mayHave.Contains((got[k].Object, got[k].Shape)), Is.True, $"{what}: object {got[k].Object} shape {got[k].Shape} is not within the sphere");
            }
            Assert.That(m, Is.EqualTo(mustHave.Count), what + ": every conductor whose envelope meets the sphere is returned");
            return got;
        }

        /// <summary>The ends the hang records say: (object, port index) at the A end (from) and the B end (to) of wire w.</summary>
        private static (int Object, int Port) Hung(Dictionary<string, object> hang, string side, int w)
        {
            int pole = (int)(double)hang[side];
            string id = (string)List(hang[side + "_ports"])[w];
            List<object> ports = List(Meta(Obj(Rows[pole]))["ports"]);
            for (int p = 0; p < ports.Count; p++)
                if ((string)Obj(ports[p])["id"] == id)
                    return (pole, p);
            Assert.Fail($"pole {pole} has no port {id}");
            return default;
        }

        /// <summary>The span (and its wire shape) whose hang puts an end on (pole, port) from `side`, other than `self`; (−1, −1) for none.</summary>
        private static (int Object, int Shape) Sharing((int Object, int Port) port, int self)
        {
            foreach (object r in Rows)
            {
                var row = Obj(r);
                if (!row.TryGetValue("hang", out object h) || (int)(double)row["id"] == self)
                    continue;
                var hang = Obj(h);
                for (int w = 0; w < List(hang["from_ports"]).Count; w++)
                    if (Hung(hang, "from", w) == port || Hung(hang, "to", w) == port)
                        return ((int)(double)row["id"], List(Meta(row)["collision"]).Count + w);
            }
            return (-1, -1);
        }

        private static int Find(string variant, int occurrence = 0)
        {
            for (int obj = 0; obj < MapWorldTests.World.PlacedObjects; obj++)
                if (MapWorldTests.World.ObjectName(obj) == variant && occurrence-- == 0)
                    return obj;
            Assert.Fail(variant + " is not placed");
            return -1;
        }

        // ---- The range's spans and their ends ------------------------------------------------------------------------

        [Test]
        public void TheRangesSpansAndTheirEnds_AreTheBruteForceSetInCanonicalOrder_EachNamingItsPortsAndNeighbours()
        {
            MapWorld world = MapWorldTests.World;
            Assert.That(world.WorldBugs, Is.Empty, "every conductor's ends hang from ports");
            var poles = new List<int>();
            foreach (object r in Rows)
                if ((string)Obj(r)["asset"] == "L01")
                    poles.Add((int)(double)Obj(r)["id"]);
            Assert.That(poles.Count, Is.EqualTo(6));

            int returned = 0;
            for (int i = 0; i < poles.Count; i++)
            {
                Double3 head = Vec(Obj(Rows[poles[i]])["position_m"]) + new Double3(0, 6.6, 0);
                AssertBruteForce(world, head, 2, $"pole {poles[i]}'s head, r 2", out int n);
                Assert.That(n, Is.EqualTo(i == 0 || i == poles.Count - 1 ? 4 : 8), "a line's end pole holds one span's four, the others two spans'");
                AssertBruteForce(world, head, 30, $"pole {poles[i]}'s head, r 30", out int far);
                returned += n + far;
                if (i == 0)
                    continue;
                Double3 between = (head + Vec(Obj(Rows[poles[i - 1]])["position_m"]) + new Double3(0, 6.6, 0)) * 0.5 + new Double3(0, -0.6, 0);
                AssertBruteForce(world, between, 2, $"between poles {poles[i - 1]} and {poles[i]}, r 2", out int mid);
                Assert.That(mid, Is.EqualTo(4), "mid-span: its own four conductors");
                AssertBruteForce(world, between, 30, $"between poles {poles[i - 1]} and {poles[i]}, r 30", out int wide);
                returned += mid + wide;
            }
            Assert.That(returned, Is.GreaterThan(100));
            Assert.That(AssertBruteForceCount(world, new Double3(0, 0.2, 40), 30), Is.EqualTo(0), "far from the line: none");

            // Each L03_medium conductor names its two L01 ports (the hang record's) and its neighbouring span at each.
            int checkedEnds = 0, lastPole = 0;
            foreach (object r in Rows)
            {
                var row = Obj(r);
                if (!row.TryGetValue("hang", out object h))
                    continue;
                var hang = Obj(h);
                int span = (int)(double)row["id"];
                Conductor[] got = Query(world, (world.Geometry(span, 0, out ShapeGeometry g) ? g.Center : default), 3, 64, out _);
                for (int w = 0; w < 4; w++)
                {
                    int shape = List(Meta(row)["collision"]).Count + w;
                    Conductor c = Array.Find(got, x => x.Object == span && x.Shape == shape);
                    Assert.That(c.Object == span && c.Shape == shape, Is.True, $"span {span} wire {w} near its chord's midpoint");
                    foreach ((WireEnd end, string side) in new[] { (c.A, "from"), (c.B, "to") })
                    {
                        (int Object, int Port) port = Hung(hang, side, w);
                        Assert.That((end.Object, end.Port, end.Kind), Is.EqualTo((port.Object, port.Port, FittingKind.PoleHook)), $"span {span} wire {w} {side}");
                        Assert.That(world.ObjectName(end.Object), Does.StartWith("L01_"));
                        (int Object, int Shape) next = Sharing(port, span);
                        Assert.That((end.NeighbourObject, end.NeighbourShape), Is.EqualTo(next), $"span {span} wire {w} {side}'s neighbour");
                        lastPole += next.Object < 0 ? 1 : 0;
                        checkedEnds++;
                    }
                }
            }
            Assert.That(checkedEnds, Is.EqualTo(5 * 4 * 2));
            Assert.That(lastPole, Is.EqualTo(8), "none at the line's two last poles, four conductors each");

            // A buffer too small holds the canonical prefix, and the true count comes back.
            Double3 centre = (Vec(Obj(Rows[poles[2]])["position_m"]) + Vec(Obj(Rows[poles[3]])["position_m"])) * 0.5 + new Double3(0, 6, 0);
            Conductor[] big = Query(world, centre, 30, 64, out int all);
            Conductor[] small = Query(world, centre, 30, 3, out int count);
            Assert.That(all, Is.EqualTo(12), "30 m from mid-span: its own span and the two beside it");
            Assert.That(count, Is.EqualTo(all));
            for (int k = 0; k < small.Length; k++)
                Assert.That((small[k].Object, small[k].Shape, small[k].A.Port, small[k].Points.Length), Is.EqualTo((big[k].Object, big[k].Shape, big[k].A.Port, big[k].Points.Length)), "prefix " + k);
            Assert.That(world.WiresNear(centre, 30, Span<Conductor>.Empty), Is.EqualTo(all));
            Assert.That(new FloorWorld(0, "meadow_sod", "steel").WiresNear(centre, 30, big), Is.EqualTo(0), "the floor stub has none");
        }

        private static int AssertBruteForceCount(IWorldQuery world, Double3 c, double r)
        {
            AssertBruteForce(world, c, r, $"at ({c.X}, {c.Y}, {c.Z}), r {r}", out int n);
            return n;
        }

        [Test]
        public void WiresNear_AllocatesNothing()
        {
            MapWorld world = MapWorldTests.World;
            world.Geometry(Find("L03_medium", 1), 0, out ShapeGeometry g);
            var results = new Conductor[64];
            world.WiresNear(g.Center, 2, results);
            long before = AllocProbe.Before();
            for (int i = 0; i < 100; i++)
                world.WiresNear(g.Center + new Double3(0.2 * i, 0, 0), i % 2 == 0 ? 2 : 30, results);
            // Is.Zero, not Is.EqualTo(0): .NET's JIT boxes that 0 (24 B) before the closing count is read.
            Assert.That(AllocProbe.After(before), Is.Zero, AllocProbe.Report());
        }

        [Test]
        public void AnEndWithNoPortWithin5mm_IsAWorldBugNamingTheWire()
        {
            // The line's last pole moved 10 mm across the line: the last span's four B ends lie 10 mm off their seats.
            int span = Find("L03_medium", 4), last = (int)(double)Obj(Obj(Rows[span])["hang"])["to"];
            Assert.That(MapWorldTests.World.ObjectName(last), Is.EqualTo("L01_straight"));
            string row = $"{{\"id\": {last}, \"group\": \"line\", \"asset\": \"L01\", \"variant\": \"L01_straight\", \"class\": \"lines\", \"position_m\": [0.0, 0.0, -34.0]";
            string placements = MapWorldTests.Placements();
            StringAssert.Contains(row, placements);
            var world = new MapWorld(placements.Replace(row, row.Replace("-34.0]", "-33.99]")), MapWorldTests.ReadModel, MapWorldTests.Materials());
            foreach (string bug in world.WorldBugs)
                TestContext.Out.WriteLine(bug);
            Assert.That(world.WorldBugs.Length, Is.EqualTo(4));
            for (int w = 0; w < 4; w++)
                StringAssert.Contains($"instance {span} (L03_medium) wire {w}: end B", world.WorldBugs[w]);
            world.Geometry(span, 0, out ShapeGeometry g);
            Conductor[] got = Query(world, g.Center, 3, 16, out int n);
            Assert.That(n, Is.EqualTo(4));
            for (int k = 0; k < n; k++)
            {
                Assert.That((got[k].B.Object, got[k].B.Port, got[k].B.Kind, got[k].B.NeighbourObject), Is.EqualTo((-1, -1, FittingKind.None, -1)), "no port");
                Assert.That(got[k].A.Kind, Is.EqualTo(FittingKind.PoleHook), "its A end is as before");
            }
        }

        [Test]
        public void AWireRecordWithoutAPositiveBendingStiffness_IsRefusedNamingTheField()
        {
            // p7d-tether 1.4 writes bending_stiffness_n_m2 in every wire record; the loader requires it above 0.
            string placements = MapWorldTests.Placements();
            const string field = "\"bending_stiffness_n_m2\": 1.75345";
            StringAssert.Contains(field, MapWorldTests.ReadModel("lines/L03/L03_medium.assetmeta.json"));
            foreach (string with in new[] { "", "\"bending_stiffness_n_m2\": 0.0" })
            {
                string edit = with.Length == 0 ? "" : with + ",";
                Func<string, string> read = path => path.EndsWith("L03_medium.assetmeta.json", StringComparison.Ordinal)
                    ? MapWorldTests.ReadModel(path).Replace(field + ",", edit) : MapWorldTests.ReadModel(path);
                var e = Assert.Throws<FormatException>(() => new MapWorld(placements, read, MapWorldTests.Materials()), with);
                StringAssert.Contains("bending_stiffness_n_m2", e.Message);
                TestContext.Out.WriteLine(e.Message);
            }
        }

        // ---- The filter -------------------------------------------------------------------------------------------------

        [Test]
        public void TheFilter_LeavesTheConductorOutOfContactsAndRays_AndOffEveryCallerSeesItAsBefore()
        {
            MapWorld world = MapWorldTests.World;
            int line = Find("L03_medium", 1);
            ReadOnlySpan<Double3> points = world.WirePoints(line, 0);
            Double3 low = points[40]; // mid-span, its lowest point
            var across = new Double3(0, 0, 0.15);
            var up = new Double3(0, 0.1, 0);
            // A 10 mm capsule across the conductor swept down through it, and a ray down onto it.
            var previous = new Capsule(low + up - across, low + up + across, 0.01);
            var current = new Capsule(low - up - across, low - up + across, 0.01);
            var off = new StaticContact[16];
            var plain = new StaticContact[16];
            var on = new StaticContact[16];
            int nOff = world.StaticContacts(previous, current, 0.05, off, QueryFilter.None);
            int nPlain = world.StaticContacts(previous, current, 0.05, plain);
            int nOn = world.StaticContacts(previous, current, 0.05, on, QueryFilter.NoWires);
            Assert.That(nOff, Is.EqualTo(1));
            Assert.That((off[0].Object, off[0].Shape), Is.EqualTo((line, 0)), "off: the conductor");
            Assert.That(off[0].Time, Is.LessThan(1), "its first touch, latched");
            Assert.That(off[0].WireParam, Is.EqualTo(0.5).Within(0.01));
            Assert.That(nPlain, Is.EqualTo(nOff));
            Assert.That(plain[0].Point.Y.Equals(off[0].Point.Y) && plain[0].Time.Equals(off[0].Time) && plain[0].Distance.Equals(off[0].Distance), Is.True,
                "the default is the filter off, bit for bit");
            Assert.That(nOn, Is.EqualTo(0), "on: no wire, and nothing else lies there");

            var rays = new[] { new Ray(new Double3(low.X, 10, low.Z), new Double3(0, -1, 0)) };
            var hitOff = new RayHit[1];
            var hitPlain = new RayHit[1];
            var hitOn = new RayHit[1];
            world.Raycast(rays, 20, hitOff, QueryFilter.None);
            world.Raycast(rays, 20, hitPlain);
            world.Raycast(rays, 20, hitOn, QueryFilter.NoWires);
            Assert.That((hitOff[0].Object, hitOff[0].Shape), Is.EqualTo((line, 0)), "off: the conductor's top");
            Assert.That(hitOff[0].Distance, Is.EqualTo(10 - low.Y - 0.0045).Within(1e-6));
            Assert.That(hitPlain[0].Distance.Equals(hitOff[0].Distance) && hitPlain[0].Object == hitOff[0].Object, Is.True, "the default is the filter off");
            Assert.That(hitOn[0].Object, Is.EqualTo(WorldIds.Ground), "on: what lies beyond, the field");
            Assert.That(hitOn[0].Distance, Is.EqualTo(10).Within(Exact));
            Assert.That(world.SurfaceName(hitOn[0].Surface), Is.EqualTo("belt_straw"));

            // The filter leaves every other shape as it is: a ray down onto a pole's top.
            int pole = Find("L01_straight");
            var down = new[] { new Ray(Vec(Obj(Rows[pole])["position_m"]) + new Double3(0, 9, 0), new Double3(0, -1, 0)) };
            world.Raycast(down, 5, hitOff);
            world.Raycast(down, 5, hitOn, QueryFilter.NoWires);
            Assert.That(hitOff[0].Object, Is.EqualTo(pole), "the pole");
            Assert.That(hitOn[0].Distance.Equals(hitOff[0].Distance) && hitOn[0].Object == hitOff[0].Object && hitOn[0].Shape == hitOff[0].Shape
                && hitOn[0].Normal.Y.Equals(hitOff[0].Normal.Y), Is.True, "the same hit, bit for bit");
        }

        // ---- Determinism ------------------------------------------------------------------------------------------------

        private static ulong RunQueries(IWorldQuery world, int seed, int count)
        {
            world.Geometry(Find("L03_medium", 2), 0, out ShapeGeometry g);
            var rng = new Random(seed);
            var results = new Conductor[64];
            var contacts = new StaticContact[32];
            var hit = new RayHit[1];
            ulong hash = WorldHash.Offset;
            void Add(ulong v)
            {
                for (int k = 0; k < 8; k++)
                    hash = (hash ^ (byte)(v >> 8 * k)) * 0x100000001B3UL;
            }
            void AddD(double v) => Add((ulong)BitConverter.DoubleToInt64Bits(v));
            for (int i = 0; i < count; i++)
            {
                Double3 c = g.Center + new Double3(60 * rng.NextDouble() - 30, 3 * rng.NextDouble() - 2, 2 * rng.NextDouble() - 1);
                int n = world.WiresNear(c, 0.1 + 29.9 * rng.NextDouble(), results);
                Add((ulong)n);
                for (int k = 0; k < Math.Min(n, results.Length); k++)
                {
                    Add((ulong)results[k].Object); Add((ulong)results[k].Shape); Add((ulong)results[k].A.Port); Add((ulong)results[k].B.NeighbourObject);
                    AddD(results[k].EdgeRadius); AddD(results[k].Points.Span[results[k].Points.Length / 2].Y);
                }
                Double3 a = c + new Double3(0, 0, -0.1), b = c + new Double3(0, 0, 0.1), move = new Double3(0, -0.3, 0);
                int m = world.StaticContacts(new Capsule(a, b, 0.01), new Capsule(a + move, b + move, 0.01), 0.05, contacts, (QueryFilter)(i & 1));
                Add((ulong)m);
                for (int k = 0; k < Math.Min(m, contacts.Length); k++)
                {
                    Add((ulong)contacts[k].Object); Add((ulong)contacts[k].Shape); AddD(contacts[k].Time); AddD(contacts[k].Point.Y);
                }
                world.Raycast(new[] { new Ray(c, new Double3(0, -1, 0)) }, 10, hit, (QueryFilter)(i & 1));
                AddD(hit[0].Distance); Add((ulong)hit[0].Object); Add((ulong)hit[0].Shape);
            }
            return hash;
        }

        [Test]
        public void Concurrency_FourThreadsOnOneWorldGetTheBitsOfOne()
        {
            MapWorld world = MapWorldTests.World;
            const int count = 2000;
            ulong alone = RunQueries(world, 9, count);
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
                            results[me] = RunQueries(world, 9, count);
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
