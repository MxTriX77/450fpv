using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7a-ground 1.9: MicroDetailNear and Feature on the test range (the world-query spec's "Elements near a point" and "A
    // pit names its feature"). The expected sets are brute force over every placed instance's sidecar, read independently
    // (TestJson, double.Parse) and placed by Math.Sin and Cos of the row's yaw; an element whose distance lies within
    // Ambiguous of the sphere's reach is left out of the comparison (it never happened on these spheres: printed).
    public sealed class MicroDetailTests
    {
        private const double Ambiguous = 1e-9; // two parsers and two rotations: a few ulps of a few hundred metres
        private const double Exact = 1e-9;

        /// <summary>One element as the files give it, placed independently.</summary>
        private sealed class Expected
        {
            public int Object, Index, PointCount, Parent, ParentShape;
            public MicroKind Kind;
            public bool Anchored;
            public double Diameter, Area;
            public Double3[] World;
        }

        private static List<Expected> _all;

        /// <summary>Every placed instance's elements, in canonical order.</summary>
        private static List<Expected> All => _all ?? (_all = ReadAll(MapWorldTests.Placements(), MapWorldTests.ReadModel));

        private static Dictionary<string, object> Obj(object n) => (Dictionary<string, object>)n;

        private static List<object> List(object n) => (List<object>)n;

        private static Double3 Vec(object n) => new Double3((double)List(n)[0], (double)List(n)[1], (double)List(n)[2]);

        private static List<Expected> ReadAll(string placements, Func<string, string> readModel)
        {
            var all = new List<Expected>();
            var sidecars = new Dictionary<string, List<object>>(StringComparer.Ordinal);
            foreach (object row in List(Obj(TestJson.Parse(placements))["instances"]))
            {
                var r = Obj(row);
                string dir = (string)r["class"] + "/" + (string)r["asset"] + "/";
                var meta = Obj(TestJson.Parse(readModel(dir + (string)r["variant"] + ".assetmeta.json")));
                if (!meta.TryGetValue("micro_elements", out object record))
                    continue;
                string file = dir + (string)Obj(record)["file"];
                if (!sidecars.TryGetValue(file, out List<object> elements))
                    sidecars.Add(file, elements = List(Obj(TestJson.Parse(readModel(file)))["elements"]));
                Double3 at = Vec(r["position_m"]);
                double yaw = (double)r["yaw_deg"] * Math.PI / 180, c = Math.Cos(yaw), s = Math.Sin(yaw);
                for (int i = 0; i < elements.Count; i++)
                {
                    var e = Obj(elements[i]);
                    List<object> points = List(e["points"]);
                    var x = new Expected
                    {
                        Object = (int)(double)r["id"], Index = i, PointCount = points.Count, Parent = (int)(double)e["parent"],
                        ParentShape = (int)(double)e["parent_shape"], Anchored = (bool)e["anchored"], Diameter = (double)e["diameter_m"],
                        Area = e.TryGetValue("area_m2", out object area) ? (double)area : 0,
                        Kind = (string)e["kind"] == "straw" ? MicroKind.Straw : (string)e["kind"] == "stem" ? MicroKind.Stem
                            : (string)e["kind"] == "twig" ? MicroKind.Twig : MicroKind.Leaf,
                        World = new Double3[points.Count],
                    };
                    for (int k = 0; k < points.Count; k++)
                    {
                        Double3 p = Vec(points[k]); // Unity's yaw: +Z turns toward +X
                        x.World[k] = new Double3(at.X + p.X * c + p.Z * s, at.Y + p.Y, at.Z - p.X * s + p.Z * c);
                    }
                    all.Add(x);
                }
            }
            return all;
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

        private static MicroElement[] Query(IWorldQuery world, Double3 c, double r, MicroKind kinds, int buffer, out int count)
        {
            var results = new MicroElement[buffer];
            count = world.MicroDetailNear(c, r, kinds, results);
            return results;
        }

        /// <summary>The world's answer against brute force: the same set in canonical order, each field the record's.</summary>
        private static int AssertBruteForce(IWorldQuery world, List<Expected> all, Double3 c, double r, MicroKind kinds, string what,
            Func<Expected, bool> excluded = null)
        {
            MicroElement[] got = Query(world, c, r, kinds, 32768, out int n);
            Assert.That(n, Is.LessThanOrEqualTo(got.Length), what + ": buffer");
            var mustHave = new List<Expected>();
            var mayHave = new HashSet<ulong>();
            int ambiguous = 0;
            foreach (Expected e in all)
            {
                if ((e.Kind & kinds) == 0 || excluded != null && excluded(e))
                    continue;
                double gap = Distance(c, e.World) - (r + e.Diameter / 2);
                ulong id = (ulong)e.Object << 32 | (uint)e.Index;
                if (gap < -Ambiguous)
                    mustHave.Add(e);
                else if (gap <= Ambiguous)
                {
                    ambiguous++;
                    mayHave.Add(id);
                }
            }
            TestContext.Out.WriteLine($"{what}: {n} elements (brute force {mustHave.Count}, {ambiguous} on the reach within {Ambiguous} m)");
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                if (k > 0)
                    Assert.That(got[k].Id, Is.GreaterThan(got[k - 1].Id), what + ": canonical order");
                if (m < mustHave.Count && got[k].Id == ((ulong)mustHave[m].Object << 32 | (uint)mustHave[m].Index))
                {
                    Expected e = mustHave[m++];
                    Assert.That(got[k].Object, Is.EqualTo(e.Object));
                    Assert.That(got[k].Index, Is.EqualTo(e.Index));
                    Assert.That((got[k].Kind, got[k].Anchored, (int)got[k].PointCount, got[k].Parent, got[k].ParentShape),
                        Is.EqualTo((e.Kind, e.Anchored, e.PointCount, e.Parent, e.ParentShape)), what + " element " + e.Index);
                    Assert.That(got[k].Diameter, Is.EqualTo(e.Diameter).Within(Exact));
                    Assert.That(got[k].Area, Is.EqualTo(e.Area).Within(Exact));
                    Double3[] p = { got[k].P0, got[k].P1, got[k].P2 };
                    for (int j = 0; j < 3; j++)
                    {
                        Double3 want = e.World[Math.Min(j, e.PointCount - 1)];
                        Assert.That((p[j] - want).Length(), Is.LessThan(Exact), what + $" element {e.Index} point {j}");
                    }
                    continue;
                }
                Assert.That(mayHave.Contains(got[k].Id), Is.True, $"{what}: element {got[k].Index} of object {got[k].Object} is not within the sphere");
            }
            Assert.That(m, Is.EqualTo(mustHave.Count), what + ": every element within the sphere is returned");
            return n;
        }

        private static int Find(string variant, int occurrence = 0)
        {
            for (int obj = 0; obj < MapWorldTests.World.PlacedObjects; obj++)
                if (MapWorldTests.World.ObjectName(obj) == variant && occurrence-- == 0)
                    return obj;
            Assert.Fail(variant + " is not placed");
            return -1;
        }

        private static List<object> _rows;

        private static Dictionary<string, object> Row(int obj) =>
            Obj((_rows ?? (_rows = List(Obj(TestJson.Parse(MapWorldTests.Placements()))["instances"])))[obj]);

        private static Double3 Position(int obj) => Vec(Row(obj)["position_m"]);

        private delegate ReadOnlySpan<byte> SpanGetter();

        // ---- The lowest-bit table ----------------------------------------------------------------------------------

        [Test]
        public void TheLowestBitLiterals_AreLowestTablesOutput()
        {
            const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var literals = (SpanGetter)Delegate.CreateDelegate(typeof(SpanGetter),
                typeof(MapWorld).GetProperty("Lowest", Private).GetGetMethod(true));
            var table = (byte[])typeof(MapWorld).GetMethod("LowestTable", Private).Invoke(null, null);
            Assert.That(literals().ToArray(), Is.EqualTo(table));
        }

        // ---- Elements near a point ---------------------------------------------------------------------------------

        [Test]
        public void ElementsNearAPoint_AreTheBruteForceSetInCanonicalOrder()
        {
            MapWorld world = MapWorldTests.World;
            int patch = Find("S04_patch"), crown = Find("V01_L");
            Double3 inPatch = Position(patch) + new Double3(0.3, 0.2, -0.4);
            Double3 between = (Position(patch) + Position(Find("S04_patch", 2))) * 0.5 + new Double3(0, 0.2, 0); // two patches 12 m apart
            Double3 inCrown = Position(crown) + new Double3(0, 4, 0);
            var plane = new Double3(0, 0.2, 0); // between the belts' two rows of patches
            const MicroKind noStems = MicroKind.All & ~MicroKind.Stem;

            int all = AssertBruteForce(world, All, inPatch, 2, MicroKind.All, "S04, every kind");
            Assert.That(all, Is.GreaterThan(1000));
            int masked = AssertBruteForce(world, All, inPatch, 2, noStems, "S04, stems masked");
            Assert.That(masked, Is.LessThan(all));
            Assert.That(AssertBruteForce(world, All, inPatch, 2, MicroKind.Straw, "S04, straws only"), Is.LessThan(masked));
            Assert.That(AssertBruteForce(world, All, inPatch, 0.3, MicroKind.All, "S04, r 0.3"), Is.GreaterThan(0));
            int across = AssertBruteForce(world, All, between, 2, MicroKind.All, "between two patches");
            Assert.That(across, Is.GreaterThan(0));
            MicroElement[] two = Query(world, between, 2, MicroKind.All, across, out _);
            Assert.That(two[0].Object, Is.LessThan(two[across - 1].Object), "both patches' elements, the first patch's first");
            // V01_L's crown has no elements until p7a-ground 1.7, and nothing is placed over the plane there.
            Assert.That(AssertBruteForce(world, All, inCrown, 2, MicroKind.All, "V01_L's crown"), Is.EqualTo(0));
            Assert.That(AssertBruteForce(world, All, plane, 2, MicroKind.All, "over the plane"), Is.EqualTo(0));

            MicroElement[] big = Query(world, inPatch, 2, MicroKind.All, 32768, out int n);
            MicroElement[] again = Query(world, inPatch, 2, MicroKind.All, 32768, out int n2);
            Assert.That(n2, Is.EqualTo(n));
            for (int k = 0; k < n; k++)
                Assert.That(again[k].Id == big[k].Id && again[k].P0.X.Equals(big[k].P0.X) && again[k].P2.Z.Equals(big[k].P2.Z), Is.True, "the same query, the same ids and bits");

            // The masked kind is absent; a straw rooted outside the sphere crosses it.
            MicroElement[] noStem = Query(world, inPatch, 2, noStems, 32768, out int ns);
            for (int k = 0; k < ns; k++)
                Assert.That(noStem[k].Kind, Is.Not.EqualTo(MicroKind.Stem));
            bool crossing = false;
            for (int k = 0; k < n; k++)
                crossing |= big[k].Kind == MicroKind.Straw && (big[k].P0 - inPatch).Length() > 2 + big[k].Diameter / 2;
            Assert.That(crossing, Is.True, "a straw rooted outside the sphere but crossing it is returned");

            // A buffer too small holds the canonical prefix, and the true count comes back.
            MicroElement[] small = Query(world, inPatch, 2, MicroKind.All, 7, out int m);
            Assert.That(m, Is.EqualTo(n));
            for (int k = 0; k < small.Length; k++)
                Assert.That(small[k].Id == big[k].Id && small[k].P1.Y.Equals(big[k].P1.Y), Is.True, "prefix " + k);
            Assert.That(world.MicroDetailNear(inPatch, 2, MicroKind.All, Span<MicroElement>.Empty), Is.EqualTo(n));
            Assert.That(world.MicroDetailNear(inPatch, 2, MicroKind.None, big), Is.EqualTo(0));
        }

        [Test]
        public void MicroDetailNear_AllocatesNothing()
        {
            MapWorld world = MapWorldTests.World;
            Double3 inPatch = Position(Find("S04_patch")) + new Double3(0.3, 0.2, -0.4);
            var results = new MicroElement[8192];
            world.MicroDetailNear(inPatch, 2, MicroKind.All, results);
            long before = AllocProbe.Before();
            for (int i = 0; i < 100; i++)
                world.MicroDetailNear(inPatch + new Double3(0.01 * i, 0, 0), 2, MicroKind.All, results);
            // Is.Zero, not Is.EqualTo(0): .NET's JIT boxes that 0 (24 B) before the closing count is read.
            Assert.That(AllocProbe.After(before), Is.Zero, AllocProbe.Report());
        }

        // ---- Nothing rooted in a hole --------------------------------------------------------------------------------

        /// <summary>The test range with one more hole band, of `instance`, across `from`–`to` (y 0) at `width`.</summary>
        private static string WithBand(int instance, Double3 from, Double3 to, double width)
        {
            string placements = MapWorldTests.Placements();
            const string holes = "\"holes\": [";
            int at = placements.IndexOf(holes, StringComparison.Ordinal) + holes.Length;
            string band = FormattableString.Invariant(
                $"{{\"instance\": {instance}, \"record\": \"test\", \"centre_line_m\": [[{from.X}, 0.0, {from.Z}], [{to.X}, 0.0, {to.Z}]], \"band_width_m\": {width}, \"top_width_m\": 0.5, \"depth_m\": 1.0}}, ");
            return placements.Substring(0, at) + band + placements.Substring(at);
        }

        /// <summary>The models with two small boxes of the filler surface (belt_bare) added to `instance`'s variant at the
        /// band's ends `from` and `to`: p7c-wind's cut records close a band's ends on its object's fillers (MapWorldWind.cs).</summary>
        private static Func<string, string> WithEndFillers(int instance, Double3 from, Double3 to)
        {
            var row = Row(instance);
            Double3 at = Vec(row["position_m"]);
            double yaw = (double)row["yaw_deg"] * Math.PI / 180, c = Math.Cos(yaw), s = Math.Sin(yaw);
            string Box(Double3 w)
            {
                double dx = w.X - at.X, dz = w.Z - at.Z; // the row's frame: the inverse of the placement in Expected
                return FormattableString.Invariant(
                    $", {{\"shape\": \"box\", \"center\": [{dx * c - dz * s:0.######}, -0.05, {dx * s + dz * c:0.######}], \"size\": [0.1, 0.1, 0.1], \"rotation\": [0.0, 0.0, 0.0, 1.0], \"material\": \"surface:belt_bare\", \"landable\": false}}");
            }
            string boxes = Box(from) + Box(to);
            return Edited((string)row["variant"] + ".assetmeta.json", t =>
            {
                int close = t.LastIndexOf(']', t.IndexOf("\"gaps\":", StringComparison.Ordinal));
                return t.Substring(0, close) + boxes + t.Substring(close);
            });
        }

        private static double BandDistance(Double3 p, Double3 a, Double3 b)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z, px = p.X - a.X, pz = p.Z - a.Z;
            double t = Math.Max(0, Math.Min(1, (px * dx + pz * dz) / (dx * dx + dz * dz)));
            double ex = px - dx * t, ez = pz - dz * t;
            return Math.Sqrt(ex * ex + ez * ez);
        }

        [Test]
        public void NothingIsRootedInAnotherInstancesHoleBand_ButAnInstancesOwnBandKeepsItsElements()
        {
            int patch = Find("S04_patch"), shaft = Find("Z04_cellar_shaft");
            Double3 c = Position(patch), from = c + new Double3(-2, 0, 0), to = c + new Double3(2, 0, 0), query = c + new Double3(0, 0.2, 0);
            const double width = 1.0;
            var world = new MapWorld(WithBand(shaft, from, to, width), WithEndFillers(shaft, from, to), MapWorldTests.Materials());
            bool Rooted(Expected e) => e.Object == patch && e.Parent < 0 && e.ParentShape < 0 && BandDistance(e.World[0], from, to) <= width / 2;
            int dropped = 0;
            foreach (Expected e in All)
                dropped += Rooted(e) ? 1 : 0;
            int n = AssertBruteForce(world, All, query, 2, MicroKind.All, "a shaft's band across S04", Rooted);
            int before = MapWorldTests.World.MicroDetailNear(query, 2, MicroKind.All, Span<MicroElement>.Empty);
            TestContext.Out.WriteLine($"{dropped} of the patch's elements rooted in the band; {before} near the query before, {n} after");
            Assert.That(dropped, Is.GreaterThan(50));
            Assert.That(n, Is.LessThan(before));
            MicroElement[] got = Query(world, query, 2, MicroKind.All, 32768, out n);
            bool over = false;
            for (int k = 0; k < n; k++)
            {
                double root = BandDistance(got[k].P0, from, to);
                Assert.That(root, Is.GreaterThan(width / 2 - 1e-9), "nothing returned is rooted in the band");
                over |= BandDistance(got[k].P1, from, to) < width / 2 || BandDistance(got[k].P2, from, to) < width / 2;
            }
            Assert.That(over, Is.True, "an element rooted outside the band reaching over it is kept");

            // The same band as the patch's own: its elements stay (a trench's floor straws lie in its own run).
            var own = new MapWorld(WithBand(patch, from, to, width), WithEndFillers(patch, from, to), MapWorldTests.Materials());
            Assert.That(own.MicroDetailNear(query, 2, MicroKind.All, Span<MicroElement>.Empty), Is.EqualTo(before));
        }

        // ---- The sidecar is the metadata's -------------------------------------------------------------------------

        private static Func<string, string> Edited(string suffix, Func<string, string> edit) =>
            path => path.EndsWith(suffix, StringComparison.Ordinal) ? edit(MapWorldTests.ReadModel(path)) : MapWorldTests.ReadModel(path);

        [Test]
        public void ASidecarThatIsNotTheMetadatasIsRefused_AndCrLfReadsAsLf()
        {
            string placements = MapWorldTests.Placements();
            string[] materials = MapWorldTests.Materials();
            // One digit of one record changed: the bytes no longer hash to the metadata's SHA-256.
            var e = Assert.Throws<FormatException>(() => new MapWorld(placements,
                Edited("S04_patch.elements.json", t => t.Replace("0.004055,", "0.004056,")), materials));
            StringAssert.Contains("SHA-256", e.Message);
            // The counts the metadata names are the sidecar's.
            e = Assert.Throws<FormatException>(() => new MapWorld(placements,
                Edited("S04_patch.assetmeta.json", t => t.Replace("\"twig\": 72", "\"twig\": 71")), materials));
            StringAssert.Contains("counts", e.Message);
            // A CRLF checkout of the sidecar is the same elements.
            var crlf = new MapWorld(placements, Edited("S04_patch.elements.json", t => t.Replace("\n", "\r\n")), materials);
            Assert.That(crlf.ContentHash, Is.EqualTo(MapWorldTests.World.ContentHash));
            Double3 at = Position(Find("S04_patch")) + new Double3(1, 0.1, 1);
            MicroElement[] a = Query(crlf, at, 1, MicroKind.All, 8192, out int na), b = Query(MapWorldTests.World, at, 1, MicroKind.All, 8192, out int nb);
            Assert.That(na, Is.EqualTo(nb));
            for (int k = 0; k < na; k++)
                Assert.That(a[k].Id == b[k].Id && a[k].P2.X.Equals(b[k].P2.X), Is.True);
        }

        // ---- A pit names its feature ------------------------------------------------------------------------------

        private const string Pit = "\"features\": [{\"kind\": \"pitfall\", \"id\": 3, \"center\": [1.0, -0.2, 2.0], \"radius_m\": 0.25, "
            + "\"depth_m\": 0.15, \"shapes\": [4, 5]}],\n  ";

        private static Func<string, string> WithFeatures(string features) =>
            Edited("S04_patch.assetmeta.json", t => t.Replace("\"micro_elements\"", features + "\"micro_elements\""));

        [Test]
        public void APitNamesItsFeature()
        {
            MapWorld world = MapWorldTests.World;
            int patch = Find("S04_patch");
            // The committed S04 has no pits yet (p7a-ground 1.6 adds them): no shape names a feature.
            for (int s = 0; world.Geometry(patch, s, out _); s++)
                Assert.That(world.Feature(patch, s, out _), Is.False, "S04 shape " + s);

            // A pit naming two of the patch's shapes (S04's metadata with a features record, as 1.6 writes them).
            var pitted = new MapWorld(MapWorldTests.Placements(), WithFeatures(Pit), MapWorldTests.Materials());
            Assert.That(pitted.ContentHash, Is.Not.EqualTo(world.ContentHash), "the features are in the content hash");
            var row = Row(patch);
            double yaw = (double)row["yaw_deg"] * Math.PI / 180;
            Double3 at = Vec(row["position_m"]), centre = at + new Double3(1.0 * Math.Cos(yaw) + 2.0 * Math.Sin(yaw), -0.2, -1.0 * Math.Sin(yaw) + 2.0 * Math.Cos(yaw));
            foreach (int s in new[] { 4, 5 })
            {
                Assert.That(pitted.Feature(patch, s, out GroundFeature f), Is.True, "pit shape " + s);
                Assert.That((f.Id, f.Radius, f.Depth), Is.EqualTo((3, 0.25, 0.15)));
                Assert.That((f.Center - centre).Length(), Is.LessThan(Exact), "the pit's centre placed at its row's pose");
            }
            Assert.That(pitted.Feature(patch, 1, out GroundFeature none), Is.False, "a mat shape outside the pit");
            Assert.That(none.Id, Is.EqualTo(0));
            Assert.That(pitted.Feature(Find("S04_patch", 1), 4, out GroundFeature other), Is.True, "every placement of the variant");
            Assert.That(other.Id, Is.EqualTo(3));
            Assert.That(pitted.Feature(WorldIds.Ground, 0, out _), Is.False, "the ground");
            Assert.That(pitted.Feature(patch, 10000, out _), Is.False, "no such shape");
            Assert.That(pitted.Feature(-5, 4, out _), Is.False, "no such object");

            // Malformed records are refused at load.
            foreach (string bad in new[]
            {
                Pit.Replace("[4, 5]", "[4, 4]"),                                  // a shape in two features (itself twice)
                Pit.Replace("}],", "}, {\"kind\": \"pitfall\", \"id\": 4, \"center\": [0, 0, 0], \"radius_m\": 0.1, \"depth_m\": 0.1, \"shapes\": [5]}],"),
                Pit.Replace("[4, 5]", "[4, 199]"),                                // no such shape
                Pit.Replace("\"pitfall\"", "\"crater\""),                         // an unknown kind
                Pit.Replace("\"depth_m\": 0.15", "\"depth_m\": 0"),               // no depth
            })
                Assert.Throws<FormatException>(() => new MapWorld(MapWorldTests.Placements(), WithFeatures(bad), MapWorldTests.Materials()), bad);
        }

        // ---- Determinism --------------------------------------------------------------------------------------------

        private static ulong RunQueries(IWorldQuery world, int seed, int count)
        {
            Double3[] spots = { Position(Find("S04_patch")), Position(Find("S04_patch", 5)), Position(Find("S04_patch", 20)) };
            var rng = new Random(seed);
            var results = new MicroElement[4096];
            ulong hash = WorldHash.Offset;
            void Add(ulong v)
            {
                for (int k = 0; k < 8; k++)
                    hash = (hash ^ (byte)(v >> 8 * k)) * 0x100000001B3UL;
            }
            void AddD(double v) => Add((ulong)BitConverter.DoubleToInt64Bits(v));
            for (int i = 0; i < count; i++)
            {
                Double3 c = spots[i % spots.Length] + new Double3(8 * rng.NextDouble() - 4, rng.NextDouble(), 8 * rng.NextDouble() - 4);
                int n = world.MicroDetailNear(c, 0.1 + 1.9 * rng.NextDouble(), (MicroKind)(1 + i % 15), results);
                Add((ulong)n);
                for (int k = 0; k < Math.Min(n, results.Length); k++)
                {
                    Add(results[k].Id);
                    AddD(results[k].P0.X); AddD(results[k].P0.Y); AddD(results[k].P0.Z); AddD(results[k].P1.X); AddD(results[k].P2.Z);
                    AddD(results[k].Diameter);
                }
            }
            return hash;
        }

        [Test]
        public void Concurrency_FourThreadsOnOneWorldGetTheBitsOfOne()
        {
            MapWorld world = MapWorldTests.World;
            const int count = 300;
            ulong alone = RunQueries(world, 5, count);
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
                            results[me] = RunQueries(world, 5, count);
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
