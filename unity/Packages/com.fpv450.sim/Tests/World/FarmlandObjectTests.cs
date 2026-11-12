using System;
using System.Collections.Generic;
using System.IO;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The farmland's objects behind IWorldQuery (w3-farmland-ground 1.2): maps/farmland/placements.json's 1,679 trees, bushes,
    // ruins and debris with W4a's and W4b's metadata. Each row is recomputed here as Unity draws it, independently of the
    // loader: the files read by TestJson, the variant's asset from the vendor lists, Unity's TRS (position + R·(scale ⊙ local))
    // with R the quaternion's rotation matrix (not Quat.Rotate), a row mirrored in x as its _mirrored twin.
    public sealed class FarmlandObjectTests
    {
        private const double Exact = 1e-9; // two forms of one rotation over coordinates of a kilometre: only rounding

        private static MapWorld World => FarmlandTests.World;

        private sealed class Row
        {
            public string Kind, Variant;
            public Double3 Position, Scale;
            public double[] R; // row-major 3 × 3
            public Dictionary<string, object> Meta;

            public Double3 Turn(Double3 v) => new Double3(R[0] * v.X + R[1] * v.Y + R[2] * v.Z, R[3] * v.X + R[4] * v.Y + R[5] * v.Z,
                R[6] * v.X + R[7] * v.Y + R[8] * v.Z);

            /// <summary>Unity's TRS: a local point placed.</summary>
            public Double3 Place(Double3 v) => Position + Turn(new Double3(v.X * Scale.X, v.Y * Scale.Y, v.Z * Scale.Z));
        }

        private static Row[] _rows;

        private static Row[] Rows => _rows ?? (_rows = ReadRows());

        private static Dictionary<string, object> Obj(object n) => (Dictionary<string, object>)n;

        private static List<object> List(object n) => (List<object>)n;

        private static Double3 Vec(object n)
        {
            var v = (List<object>)n;
            return new Double3((double)v[0], (double)v[1], (double)v[2]);
        }

        /// <summary>The rotation matrix of a quaternion (x, y, z, w), normalised first.</summary>
        private static double[] Matrix(double x, double y, double z, double w)
        {
            double n = Math.Sqrt(x * x + y * y + z * z + w * w);
            x /= n; y /= n; z /= n; w /= n;
            return new[]
            {
                1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w),
                2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w),
                2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y),
            };
        }

        private static double[] Matrix(object quaternion)
        {
            if (quaternion == null)
                return Matrix(0, 0, 0, 1);
            List<object> q = List(quaternion);
            return Matrix((double)q[0], (double)q[1], (double)q[2], (double)q[3]);
        }

        private static Row[] ReadRows()
        {
            string root = MapWorldTests.Root;
            var assets = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string list in new[] { "assets-src/vendor_plants.json", "assets-src/vendor.json" })
                foreach (KeyValuePair<string, object> a in Obj(Obj(TestJson.Parse(File.ReadAllText(Path.Combine(root, list))))["assets"]))
                    foreach (object v in List(Obj(a.Value)["variants"]))
                        assets.Add((string)v, Obj(a.Value)["class"] + "/" + a.Key);
            var metas = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            List<object> rows = List(Obj(TestJson.Parse(File.ReadAllText(Path.Combine(root, "maps/farmland/placements.json"))))["placements"]);
            var result = new Row[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                Dictionary<string, object> r = Obj(rows[i]);
                Double3 scale = Vec(r["scale"]);
                string variant = (string)r["variant"];
                if (scale.X < 0) // W4b design §2: the mirrored house is its own variant
                {
                    variant += "_mirrored";
                    scale = new Double3(-scale.X, scale.Y, scale.Z);
                }
                if (!metas.TryGetValue(variant, out Dictionary<string, object> meta))
                    metas.Add(variant, meta = Obj(TestJson.Parse(MapWorldTests.ReadModel(assets[variant] + "/" + variant + ".assetmeta.json"))));
                result[i] = new Row
                {
                    Kind = (string)r["kind"], Variant = variant, Position = Vec(r["position_m"]), Scale = scale, R = Matrix(r["rotation_xyzw"]), Meta = meta,
                };
            }
            return result;
        }

        private static double Distance(Double3 a, Double3 b) => (a - b).Length();

        [Test]
        public void Load_EveryRowIsItsVariantWithItsShapes()
        {
            Assert.That(World.PlacedObjects, Is.EqualTo(1679));
            var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
            int shapes = 0;
            for (int i = 0; i < Rows.Length; i++)
            {
                Assert.That(World.ObjectName(i), Is.EqualTo(Rows[i].Variant), $"row {i}");
                int n = List(Rows[i].Meta["collision"]).Count;
                Assert.That(World.Geometry(i, n - 1, out _) && !World.Geometry(i, n, out _), Is.True, $"row {i}: {n} shapes");
                kinds[Rows[i].Kind] = kinds.TryGetValue(Rows[i].Kind, out int k) ? k + 1 : 1;
                shapes += n;
            }
            Assert.That((kinds["tree"], kinds["bush"], kinds["ruin"], kinds["debris"]), Is.EqualTo((294, 1352, 5, 28)));
            Assert.That(World.ObjectName(1662), Is.EqualTo("Ruin_Walls_mirrored"), "the house W2 exports as Ruin_Walls at scale x = -1");
            Assert.That(World.ObjectName(1672), Is.EqualTo("Ruin_Walls"));
            TestContext.Out.WriteLine($"  {Rows.Length} rows, {shapes} shapes");
        }

        [Test]
        public void PoseParity_EveryShapeIsItsRowsTransformOfItsMetadata()
        {
            // A sphere's centre and a capsule's two core ends placed by the row's TRS, radii by the scale's x (w); a box's
            // centre placed, its axes R times its own rotation's (scale 1 on every box row).
            double worst = 0, worstAxis = 0, worstRadius = 0, approximation = 0, woodApproximation = 0;
            int spheres = 0, capsules = 0, boxes = 0;
            for (int i = 0; i < Rows.Length; i++)
            {
                Row row = Rows[i];
                List<object> collision = List(row.Meta["collision"]);
                for (int s = 0; s < collision.Count; s++)
                {
                    Dictionary<string, object> c = Obj(collision[s]);
                    Assert.That(World.Geometry(i, s, out ShapeGeometry g), Is.True);
                    double[] local = Matrix(c.TryGetValue("rotation", out object q) ? q : null);
                    Double3 centre = row.Place(Vec(c["center"]));
                    double r = c.TryGetValue("radius", out object radius) ? (double)radius : 0, w = row.Scale.X, h = row.Scale.Y;
                    switch ((string)c["shape"])
                    {
                        case "sphere":
                            spheres++;
                            worst = Math.Max(worst, Distance(g.Center, centre));
                            worstRadius = Math.Max(worstRadius, Math.Abs(g.Radius - r * w));
                            approximation = Math.Max(approximation, r * Math.Abs(h - w));
                            break;
                        case "capsule":
                        {
                            capsules++;
                            double half = Math.Max(0, (double)c["height"] / 2 - r);
                            var axis = new Double3(local[1] * half, local[4] * half, local[7] * half); // the local rotation's y column
                            Double3 top = row.Place(Vec(c["center"]) + axis), bottom = row.Place(Vec(c["center"]) - axis);
                            worst = Math.Max(worst, Math.Max(Distance(g.Center + g.AxisY * g.HalfLength, top), Distance(g.Center - g.AxisY * g.HalfLength, bottom)));
                            worstRadius = Math.Max(worstRadius, Math.Abs(g.Radius - r * w));
                            approximation = Math.Max(approximation, r * Math.Abs(h - w));
                            if ((string)c["material"] != "twig")
                                woodApproximation = Math.Max(woodApproximation, r * Math.Abs(h - w));
                            Assert.That(Math.Abs(Double3.Dot(g.AxisX, g.AxisY)) + Math.Abs(g.AxisX.Length() - 1) + Math.Abs(g.AxisY.Length() - 1)
                                + (g.AxisZ - Double3.Cross(g.AxisX, g.AxisY)).Length(), Is.LessThan(1e-12), $"row {i} shape {s}: an orthonormal frame");
                            break;
                        }
                        case "box":
                        {
                            boxes++;
                            Assert.That((row.Scale.X, row.Scale.Y, row.Scale.Z), Is.EqualTo((1.0, 1.0, 1.0)), "premise: the boxes' rows are unscaled");
                            worst = Math.Max(worst, Distance(g.Center, centre));
                            for (int a = 0; a < 3; a++)
                            {
                                Double3 expected = row.Turn(new Double3(local[a], local[3 + a], local[6 + a]));
                                Double3 actual = a == 0 ? g.AxisX : a == 1 ? g.AxisY : g.AxisZ;
                                worstAxis = Math.Max(worstAxis, Distance(actual, expected));
                            }
                            Double3 size = Vec(c["size"]) * 0.5;
                            worstRadius = Math.Max(worstRadius, Math.Max(Math.Abs(g.HalfExtents.X - size.X), Math.Max(Math.Abs(g.HalfExtents.Y - size.Y), Math.Abs(g.HalfExtents.Z - size.Z))));
                            break;
                        }
                        default:
                            Assert.Fail($"row {i} shape {s}: a {c["shape"]}, not a farmland shape");
                            break;
                    }
                }
            }
            TestContext.Out.WriteLine($"  {spheres} spheres, {capsules} capsules, {boxes} boxes: worst point {worst:E2} m, axis {worstAxis:E2}, size {worstRadius:E2} m; "
                + $"a round shape's radius by w lies within r·|h − w| of the scaled one: at most {approximation:F4} m (wood {woodApproximation:F4} m)");
            Assert.That(worst, Is.LessThan(Exact), "every centre and core end where Unity's TRS puts it");
            Assert.That(worstAxis, Is.LessThan(Exact), "every box's axes");
            Assert.That(worstRadius, Is.LessThan(Exact), "every radius by w and box size");
        }

        [Test]
        public void TheMirroredHouse_IsRuinWallsAtScaleXMinusOne()
        {
            // The twin's bounds placed at scale 1 are Ruin_Walls' bounds placed at Unity's scale (−1, 1, 1): the same 8 corners.
            Row row = Rows[1662];
            Dictionary<string, object> plain = Obj(TestJson.Parse(MapWorldTests.ReadModel("ruins/N01/Ruin_Walls.assetmeta.json")));
            var twin = new Row { Position = row.Position, Scale = new Double3(1, 1, 1), R = row.R, Meta = row.Meta };
            var mirror = new Row { Position = row.Position, Scale = new Double3(-1, 1, 1), R = row.R, Meta = plain };
            Double3[] Corners(Row r)
            {
                Dictionary<string, object> b = Obj(r.Meta["bounds"]);
                Double3 lo = Vec(b["min"]), hi = Vec(b["max"]);
                var corners = new Double3[8];
                for (int k = 0; k < 8; k++)
                    corners[k] = r.Place(new Double3((k & 1) == 0 ? lo.X : hi.X, (k & 2) == 0 ? lo.Y : hi.Y, (k & 4) == 0 ? lo.Z : hi.Z));
                return corners;
            }
            Double3[] a = Corners(twin), m = Corners(mirror);
            double worst = 0;
            foreach (Double3 p in a)
            {
                double best = double.PositiveInfinity;
                foreach (Double3 q in m)
                    best = Math.Min(best, Distance(p, q));
                worst = Math.Max(worst, best);
            }
            TestContext.Out.WriteLine($"  the twin's bounds' corners lie within {worst * 1000:F3} mm of the mirrored house's");
            Assert.That(worst, Is.LessThan(0.001));
        }

        // ---- SnagsNear ---------------------------------------------------------------------------------------------

        private struct Placed
        {
            public int Object, Record, Segment;
            public Double3 A, B;
            public double Radius;
        }

        /// <summary>Every snag segment of the rows whose position lies within `reach` of the box, placed by Unity's TRS, its radius by w.</summary>
        private static List<Placed> PlacedSegments(double x0, double z0, double x1, double z1, double reach)
        {
            var placed = new List<Placed>();
            for (int i = 0; i < Rows.Length; i++)
            {
                Row row = Rows[i];
                if (row.Position.X < x0 - reach || row.Position.X > x1 + reach || row.Position.Z < z0 - reach || row.Position.Z > z1 + reach)
                    continue;
                if (!row.Meta.TryGetValue("snag", out object snag))
                    continue;
                List<object> records = List(snag);
                for (int r = 0; r < records.Count; r++)
                {
                    Dictionary<string, object> record = Obj(records[r]);
                    List<object> points = List(record["points"]);
                    for (int k = 0; k + 1 < points.Count; k++)
                        placed.Add(new Placed
                        {
                            Object = i, Record = r, Segment = k, A = row.Place(Vec(points[k])), B = row.Place(Vec(points[k + 1])),
                            Radius = (double)record["radius_m"] * row.Scale.X,
                        });
                }
            }
            return placed;
        }

        private static double SegmentDistance(Double3 c, Double3 a, Double3 b)
        {
            Double3 ab = b - a;
            double dd = Double3.Dot(ab, ab), t = dd > 0 ? Math.Max(0, Math.Min(1, Double3.Dot(c - a, ab) / dd)) : 0;
            return (c - (a + ab * t)).Length();
        }

        /// <summary>The spots of the object tests: the village's ruins and debris, the F6|F7 belt's crowns, a grove, a bush cluster.</summary>
        private static readonly (string Name, double X0, double Z0, double X1, double Z1, double Low, double High)[] Spots =
        {
            ("the village", 465, 573, 550, 630, -0.2, 5),
            ("the F6|F7 belt", 295, 750, 400, 770, 0, 12),
            ("the grove", 415, 422, 445, 448, 0, 14),
            ("a bush cluster", 100, 250, 150, 300, -0.1, 2.5),
        };

        private static double Ground(double x, double z)
        {
            var g = new GroundSample[1];
            World.SampleGround(new[] { new XZ(x, z) }, g);
            return g[0].Height;
        }

        /// <summary>
        /// A query's point in a spot: three in four inside the placed bounds of a row standing in it (its metadata's bounds, a
        /// random point placed by its TRS), else anywhere in the spot at its heights over the ground.
        /// </summary>
        private static Double3 Draw((string Name, double X0, double Z0, double X1, double Z1, double Low, double High) spot, Random random, int q)
        {
            double Uniform(double lo, double hi) => lo + (hi - lo) * random.NextDouble();
            if (q % 4 != 3)
            {
                var inside = new List<int>();
                for (int i = 0; i < Rows.Length; i++)
                    if (Rows[i].Position.X >= spot.X0 && Rows[i].Position.X <= spot.X1 && Rows[i].Position.Z >= spot.Z0 && Rows[i].Position.Z <= spot.Z1)
                        inside.Add(i);
                Row row = Rows[inside[random.Next(inside.Count)]];
                Dictionary<string, object> b = Obj(row.Meta["bounds"]);
                Double3 lo = Vec(b["min"]), hi = Vec(b["max"]);
                return row.Place(new Double3(Uniform(lo.X, hi.X), Uniform(lo.Y, hi.Y), Uniform(lo.Z, hi.Z)));
            }
            double x = Uniform(spot.X0, spot.X1), z = Uniform(spot.Z0, spot.Z1);
            return new Double3(x, Ground(x, z) + Uniform(spot.Low, spot.High), z);
        }

        [Test]
        public void SnagsNear_IsTheWorldSpaceTestOverEverySegmentOfTheScaledAndTiltedRows()
        {
            var random = new Random(12);
            var results = new SnagSegment[1 << 16];
            int queries = 0, returned = 0, scaled = 0, tilted = 0;
            foreach (var spot in Spots)
            {
                List<Placed> all = PlacedSegments(spot.X0, spot.Z0, spot.X1, spot.Z1, 40);
                for (int q = 0; q < 60; q++, queries++)
                {
                    Double3 centre = Draw(spot, random, q);
                    double radius = Math.Exp(Math.Log(0.05) + (Math.Log(3) - Math.Log(0.05)) * random.NextDouble());
                    int n = World.SnagsNear(centre, radius, results);
                    Assert.That(n, Is.LessThanOrEqualTo(results.Length), "premise: the buffer holds them all");
                    var got = new HashSet<(int, int, int)>();
                    for (int k = 0; k < n; k++)
                    {
                        SnagSegment s = results[k];
                        if (k > 0)
                        {
                            SnagSegment p = results[k - 1];
                            Assert.That((s.Object, s.Record, s.Segment).CompareTo((p.Object, p.Record, p.Segment)), Is.GreaterThan(0), "canonical order");
                        }
                        got.Add((s.Object, s.Record, s.Segment));
                    }
                    int expected = 0;
                    foreach (Placed p in all)
                    {
                        double d = SegmentDistance(centre, p.A, p.B), reach = radius + p.Radius;
                        bool returnedHere = got.Contains((p.Object, p.Record, p.Segment));
                        if (d <= reach - Exact)
                            Assert.That(returnedHere, Is.True, $"{spot.Name}, query {q}: row {p.Object} ({Rows[p.Object].Variant}) record {p.Record} segment {p.Segment} lies {d:F6} m off, within {reach:F6} m");
                        if (d > reach + Exact)
                            Assert.That(returnedHere, Is.False, $"{spot.Name}, query {q}: row {p.Object} record {p.Record} segment {p.Segment} lies beyond its reach");
                        expected += returnedHere ? 1 : 0;
                        if (!returnedHere)
                            continue;
                        for (int k = 0; k < n; k++)
                        {
                            SnagSegment s = results[k];
                            if (s.Object != p.Object || s.Record != p.Record || s.Segment != p.Segment)
                                continue;
                            Assert.That(Distance(s.A, p.A) + Distance(s.B, p.B), Is.LessThan(Exact), "its points where Unity's TRS puts them");
                            Assert.That(s.Radius, Is.EqualTo(p.Radius).Within(Exact), "its radius by w");
                            Row row = Rows[p.Object];
                            scaled += row.Scale.X != 1 || row.Scale.Y != 1 ? 1 : 0;
                            tilted += row.R[1] != 0 || row.R[7] != 0 ? 1 : 0;
                        }
                    }
                    Assert.That(expected, Is.EqualTo(n), $"{spot.Name}, query {q}: every segment returned is a row's");
                    returned += n;
                }
            }
            TestContext.Out.WriteLine($"  {queries} spheres of 0.05-3 m: {returned} segments, {scaled} of a scaled row, {tilted} of a tilted one");
            Assert.That(scaled, Is.GreaterThan(1000), "premise: the trees' and bushes' records");
            Assert.That(tilted, Is.GreaterThan(100), "premise: the tilted debris' rims");
        }

        // ---- StaticContacts and Raycast ----------------------------------------------------------------------------

        [Test]
        public void StaticContacts_AreEveryShapesOwn_AmongTheTreesBushesAndRuins()
        {
            var random = new Random(13);
            var all = new StaticContact[4096];
            var bare = new StaticContact[4096];
            var brute = new List<StaticContact>();
            int queries = 0, contacts = 0, grounds = 0, long_ = 0;
            foreach (var spot in Spots)
            {
                // The objects that can be near: any row within 60 m of the spot (a reach is at most 20 m long).
                var near = new List<int>();
                for (int i = 0; i < Rows.Length; i++)
                    if (Rows[i].Position.X > spot.X0 - 60 && Rows[i].Position.X < spot.X1 + 60 && Rows[i].Position.Z > spot.Z0 - 60 && Rows[i].Position.Z < spot.Z1 + 60)
                        near.Add(i);
                for (int q = 0; q < 75; q++, queries++)
                {
                    double Uniform(double lo, double hi) => lo + (hi - lo) * random.NextDouble();
                    Double3 a = Draw(spot, random, q);
                    double length = q % 3 == 0 ? Math.Exp(Uniform(Math.Log(0.5), Math.Log(20))) : Uniform(0, 0.3), heading = Uniform(0, 2 * Math.PI);
                    var b = a + new Double3(length * Math.Sin(heading), Uniform(-0.3, 0.3), length * Math.Cos(heading));
                    var current = new Capsule(a, b, q % 4 == 0 ? 0.02 : q % 4 == 1 ? 0.000125 : 0.06);
                    Capsule previous = current;
                    if (q % 2 == 1)
                    {
                        var move = new Double3(Uniform(-0.5, 0.5), Uniform(-0.3, 0.3), Uniform(-0.5, 0.5));
                        previous = new Capsule(a + move, b + move, current.Radius);
                    }
                    double margin = (q % 3) switch { 0 => 0.5, 1 => 0.05, _ => 0.1 };
                    int n = World.StaticContacts(previous, current, margin, all);
                    int m = World.StaticContacts(previous, current, margin, bare, QueryFilter.NoGround);
                    Assert.That(n, Is.LessThanOrEqualTo(all.Length), "premise: the buffer holds every contact");
                    int ground = n > 0 && all[0].Object == WorldIds.Ground ? 1 : 0;
                    Assert.That(m, Is.EqualTo(n - ground), $"{spot.Name}, query {q}: NoGround leaves the terrain out");
                    for (int k = 0; k < m; k++)
                        Assert.That(FloorWorldTests.Bits(bare[k]), Is.EqualTo(FloorWorldTests.Bits(all[k + ground])), $"{spot.Name}, query {q}, contact {k}");
                    brute.Clear();
                    foreach (int obj in near)
                        for (int shape = 0; World.Geometry(obj, shape, out _); shape++)
                            if (World.ShapeContact(obj, shape, previous, current, margin, out StaticContact c))
                                brute.Add(c);
                    Assert.That(brute.Count, Is.EqualTo(m), $"{spot.Name}, query {q}: every shape asked alone");
                    for (int k = 0; k < m; k++)
                        Assert.That(FloorWorldTests.Bits(brute[k]), Is.EqualTo(FloorWorldTests.Bits(bare[k])), $"{spot.Name}, query {q}, shape {bare[k].Object}/{bare[k].Shape}");
                    contacts += m;
                    grounds += ground;
                    long_ += length > 0.5 ? 1 : 0;
                }
            }
            TestContext.Out.WriteLine($"  {queries} queries ({long_} reaches of 0.5-20 m): {contacts} shapes' contacts, {grounds} with the terrain");
            Assert.That(contacts, Is.GreaterThan(1000), "premise: the reaches meet the objects' shapes");
        }

        [Test]
        public void Raycast_DownOverEachTreeMeetsItBeforeTheGround()
        {
            var rays = new List<Ray>();
            var trees = new List<int>();
            for (int i = 0; i < Rows.Length; i++)
            {
                if (Rows[i].Kind != "tree")
                    continue;
                trees.Add(i);
                rays.Add(new Ray(Rows[i].Position + new Double3(0, 40, 0), new Double3(0, -1, 0)));
            }
            var hits = new RayHit[rays.Count];
            var one = new RayHit[1];
            World.Raycast(rays.ToArray(), 60, hits);
            int own = 0;
            for (int k = 0; k < hits.Length; k++)
            {
                World.Raycast(rays.ToArray().AsSpan(k, 1), 60, one);
                Assert.That((one[0].Object, one[0].Shape, BitConverter.DoubleToInt64Bits(one[0].Distance)),
                    Is.EqualTo((hits[k].Object, hits[k].Shape, BitConverter.DoubleToInt64Bits(hits[k].Distance))), "batched as one by one");
                double ground = 40 - (Ground(rays[k].Origin.X, rays[k].Origin.Z) - Rows[trees[k]].Position.Y);
                Assert.That(hits[k].Object, Is.Not.EqualTo(WorldIds.Ground), $"the tree of row {trees[k]} ({Rows[trees[k]].Variant})");
                Assert.That(hits[k].Distance, Is.LessThan(ground), "before the ground");
                own += hits[k].Object == trees[k] ? 1 : 0;
            }
            TestContext.Out.WriteLine($"  {hits.Length} rays down over the trees' pivots: every one meets a tree, {own} its own");
            Assert.That(own, Is.GreaterThan(hits.Length * 9 / 10));
        }

        [Test]
        public void WindVolumes_AreThePlantsScaledBoxes()
        {
            // Each plant's upright boxes, their centre and size scaled per axis (w, h, w) and turned by the row's yaw
            // (WindVolume.Placed), heights over the pivot, cut at the ground; the ruins' come in their rows' order too.
            ReadOnlySpan<WindVolume> volumes = World.WindVolumes;
            int k = 0, plants = 0, ruins = 0;
            double worst = 0;
            for (int i = 0; i < Rows.Length; i++)
            {
                Row row = Rows[i];
                if (!row.Meta.TryGetValue("wind_volumes", out object list))
                    continue;
                if (row.Kind != "tree" && row.Kind != "bush")
                {
                    for (; k < volumes.Length && World.WindVolumeObject(k) == i; k++)
                        ruins++;
                    continue;
                }
                foreach (object v in List(list))
                {
                    Dictionary<string, object> volume = Obj(v);
                    Double3 c = Vec(volume["center"]), size = Vec(volume["size"]);
                    double top = (c.Y + size.Y / 2) * row.Scale.Y, bottom = Math.Max(0, (c.Y - size.Y / 2) * row.Scale.Y);
                    if (!(top > bottom))
                        continue;
                    Assert.That(World.WindVolumeObject(k), Is.EqualTo(i));
                    WindVolume w = volumes[k++];
                    Double3 centre = row.Place(c);
                    worst = Math.Max(worst, Math.Abs(w.X - centre.X) + Math.Abs(w.Z - centre.Z) + Math.Abs(w.TopM - top) + Math.Abs(w.BaseM - bottom)
                        + Math.Abs(w.HalfX - size.X * row.Scale.X / 2) + Math.Abs(w.HalfZ - size.Z * row.Scale.Z / 2));
                    plants++;
                }
            }
            Assert.That(k, Is.EqualTo(volumes.Length), "every volume is a row's, in row order");
            TestContext.Out.WriteLine($"  {k} wind volumes, {plants} the plants', {ruins} the ruins': the plants' worst {worst:E2} m");
            Assert.That(worst, Is.LessThan(Exact));
            Assert.That(plants, Is.GreaterThan(1600));
        }
    }
}
