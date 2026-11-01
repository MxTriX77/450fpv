using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The test range behind IWorldQuery (p6-world-query-gallery task 1.4, D-047): maps/test-range/placements.json and its
    // assets' metadata, loaded once. Expected values are analytic or read from the files independently; tolerances name
    // what limits them.
    public sealed class MapWorldTests
    {
        private const double Exact = 1e-9;    // closed forms over world coordinates of a few hundred metres: only rounding
        private const double Advance = 1e-5; // conservative advancement stops within 1 µm of the surface
        private const double Margin = 0.05;  // physics' margin (review-physics P2)
        private static readonly Double3 Up = new Double3(0, 1, 0);

        private static MapWorld _world;
        private static string _root;

        /// <summary>The repository root (the test range's placement file found from this file or the working directory).</summary>
        private static string Root => _root ?? (_root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(
            ContactScenarios.RepositoryFile("maps/test-range/placements.json")))));

        private static MapWorld World => _world ?? (_world = MapWorld.Load(Root, "test-range"));

        private static string ReadModel(string path) => File.ReadAllText(Path.Combine(Root, MapWorld.ModelsRoot, path));

        private static string Placements() => File.ReadAllText(Path.Combine(Root, "maps/test-range/placements.json"));

        private static string[] Materials() => MapWorld.MaterialNames(File.ReadAllText(Path.Combine(Root, MapWorld.MaterialsFile)));

        private static int Object(string variant, int occurrence = 0)
        {
            for (int obj = 0; obj < World.PlacedObjects; obj++)
                if (World.ObjectName(obj) == variant && occurrence-- == 0)
                    return obj;
            Assert.Fail(variant + " is not placed");
            return -1;
        }

        private static StaticContact[] Contacts(IWorldQuery world, in Capsule previous, in Capsule current, double margin = Margin)
        {
            var buffer = new StaticContact[64];
            int n = world.StaticContacts(previous, current, margin, buffer);
            Assert.That(n, Is.LessThanOrEqualTo(buffer.Length), "overflow");
            var result = new StaticContact[n];
            Array.Copy(buffer, result, n);
            return result;
        }

        private static void AssertNear(Double3 actual, Double3 expected, double tolerance, string what = "")
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(tolerance), what + " x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tolerance), what + " y");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(tolerance), what + " z");
        }

        private static void AssertCanonicalAndOnce(StaticContact[] contacts)
        {
            for (int k = 1; k < contacts.Length; k++)
                Assert.That(contacts[k - 1].Object < contacts[k].Object
                    || contacts[k - 1].Object == contacts[k].Object && contacts[k - 1].Shape < contacts[k].Shape, Is.True,
                    $"contacts {k - 1} and {k} are out of canonical order or repeat a shape");
        }

        // ---- The load ------------------------------------------------------------------------------------------------

        [Test]
        public void Load_PlacesEveryRowWithItsVariantAndTheStartPoints()
        {
            Assert.That(World.MapId, Is.EqualTo("test-range"));
            Assert.That(World.PlacedObjects, Is.EqualTo(731));
            Assert.That(World.ObjectName(17), Is.EqualTo("L01_straight"));
            Assert.That(World.ObjectName(World.PlacedObjects), Is.Null, "no runtime object yet");
            Assert.That(World.QueryVersion, Is.EqualTo(WorldHash.QueryVersion));
            Assert.That(World.StartPoints.Length, Is.EqualTo(2));
            Assert.That(World.StartPoints[1].Id, Is.EqualTo("field"));
            Assert.That(World.StartPoints[1].Stand, Is.True);
            Assert.That(World.StartPoints[1].LegsOn, Is.False);
            Assert.That(World.SurfaceName(MapWorld.SurfaceId("belt_straw")), Is.EqualTo("belt_straw"));
            Assert.That(World.Geometry(WorldIds.Ground, 0, out ShapeGeometry ground), Is.True);
            Assert.That(ground.Kind, Is.EqualTo(ShapeKind.Plane));
            Assert.That(World.SurfaceName(ground.Surface), Is.EqualTo("belt_straw"), "the plane's surface, from the file");
        }

        // ---- The swept arm (the spec's scenario) ----------------------------------------------------------------------

        [Test]
        public void SweptCapsule_ThroughL01Straight_IsOneTimberContactAtTheAnalyticFirstTouch()
        {
            int pole = Object("L01_straight");
            Assert.That(World.Geometry(pole, 0, out ShapeGeometry g), Is.True);
            // L01_straight's shape 0 (its metadata): a capsule of r 0.112895 m up the pole's foot, at the row's position.
            Assert.That(g.Kind, Is.EqualTo(ShapeKind.Capsule));
            Assert.That(g.Radius, Is.EqualTo(0.112895));
            Double3 axis = new Double3(g.Center.X, 1.25, g.Center.Z);

            // A 0.02 m capsule (a 0.1 m core across its motion) swept 0.5 m in one step through the pole at 1.25 m.
            const double r = 0.02;
            Double3 across = new Double3(0, 0, 0.05), from = axis + new Double3(-0.4, 0, 0), to = axis + new Double3(0.1, 0, 0);
            StaticContact[] c = Contacts(World, new Capsule(from - across, from + across, r), new Capsule(to - across, to + across, r));

            Assert.That(c.Length, Is.EqualTo(1));
            Assert.That(World.ObjectName(c[0].Object), Is.EqualTo("L01_straight"));
            Assert.That(c[0].Shape, Is.EqualTo(0));
            Assert.That(World.MaterialName(c[0].Material), Is.EqualTo("timber"));
            Assert.That(c[0].Surface, Is.EqualTo(WorldIds.NoSurface));
            Assert.That(c[0].WireParam, Is.EqualTo(-1));
            // The first touch: the core's middle 0.112895 + 0.02 m from the axis, after (0.4 − 0.132895) / 0.5 of the step.
            Assert.That(c[0].Time, Is.EqualTo((0.4 - 0.132895) / 0.5).Within(Advance / 0.5));
            Assert.That(c[0].Time, Is.LessThan(1));
            Assert.That(c[0].Distance, Is.EqualTo(0).Within(Advance), "the signed distance at the first touch is 0");
            AssertNear(c[0].Normal, new Double3(-1, 0, 0), Advance, "normal");
            AssertNear(c[0].Point, axis + new Double3(-0.112895, 0, 0), Advance, "point");
        }

        // ---- The ground: E01's run, S04, a band's edge ----------------------------------------------------------------

        [Test]
        public void E01Run_IsAHole_AndAFootDroppedIntoItMeetsTheFloorFiller()
        {
            int e01 = Object("E01_curving");
            // The run's centre line passes the row's position + (9.5, 0, 0.563) (placements.json, ground.holes "run").
            Assert.That(World.Geometry(e01, 0, out _), Is.True);
            var foot = new XZ(59.5, -14.587);
            var samples = new GroundSample[1];
            World.SampleGround(new[] { foot }, samples);
            Assert.That(samples[0].Flags, Is.EqualTo(GroundFlags.Hole));
            Assert.That(samples[0].Height, Is.EqualTo(double.NegativeInfinity));
            Assert.That(samples[0].Surface, Is.EqualTo(WorldIds.NoSurface));

            // A 5 mm foot sphere dropped 1.9 m in one step from 0.3 m above the field into the run's 1.5 m deep floor.
            const double r = 0.005;
            var before = new Capsule(new Double3(foot.X, 0.3, foot.Z), new Double3(foot.X, 0.3, foot.Z), r);
            var after = new Capsule(new Double3(foot.X, -1.6, foot.Z), new Double3(foot.X, -1.6, foot.Z), r);
            StaticContact[] c = Contacts(World, before, after);
            AssertCanonicalAndOnce(c);
            Assert.That(c.Length, Is.GreaterThan(0));
            int first = 0;
            for (int k = 0; k < c.Length; k++)
            {
                Assert.That(c[k].Object, Is.Not.EqualTo(WorldIds.Ground), "no plane in the patch");
                if (c[k].Time < c[first].Time)
                    first = k;
            }
            StaticContact floor = c[first];
            Assert.That(floor.Object, Is.EqualTo(e01));
            Assert.That(floor.Material, Is.EqualTo(WorldIds.NoMaterial), "a filler is a soil shape");
            Assert.That(World.SurfaceName(floor.Surface), Is.EqualTo("belt_bare"));
            Assert.That(floor.Time, Is.LessThan(1));
            Assert.That(floor.Point.Y, Is.EqualTo(-1.5).Within(1e-3), "the run's floor (depth_m 1.5)");
            Assert.That(floor.Normal.Y, Is.GreaterThan(0.99));
            Assert.That(floor.Time, Is.EqualTo((0.3 - (floor.Point.Y + r)) / 1.9).Within(Advance));
        }

        [Test]
        public void SkidSlidingAlongTheRunsFloor_IsNotLatchedByAGrazeItNeverMade()
        {
            // A skid capsule 0.1 mm above E01's run floor (y −1.5), sliding 20 mm along the run in one step: it never touches,
            // so every floor tile within the margin is the current contact (Time 1), never a first touch. The Lipschitz
            // advancement alone creeps 0.1 mm / 20 mm of the step at a time and gave up as touched (ShapeMath.Clear).
            int e01 = Object("E01_curving");
            const double r = 0.005, y = -1.5 + r + 0.0001, z = -14.587;
            var before = new Capsule(new Double3(59.4, y, z), new Double3(59.6, y, z), r);
            var after = new Capsule(new Double3(59.42, y, z), new Double3(59.62, y, z), r);
            StaticContact[] c = Contacts(World, before, after);
            StaticContact[] floor = Array.FindAll(c, k => k.Object == e01 && k.Normal.Y > 0.99);
            Assert.That(floor.Length, Is.GreaterThan(0));
            foreach (StaticContact k in floor)
            {
                Assert.That(k.Time, Is.EqualTo(1), $"shape {k.Shape}");
                Assert.That(k.Distance, Is.EqualTo(0.0001).Within(1e-9), $"shape {k.Shape}");
            }
        }

        [Test]
        public void S04Patch_IsAnsweredOnceByItsSoilAndStraw_WithoutThePlane()
        {
            int patch = Object("S04_patch");
            Assert.That(World.Geometry(patch, 0, out ShapeGeometry soil), Is.True);
            Assert.That(World.Geometry(patch, 1, out ShapeGeometry straw), Is.True);
            // S04's metadata: shape 0 the soil box (surface belt_straw, top −0.125 m), shape 1 a straw strip (top 0).
            Assert.That(World.SurfaceName(soil.Surface), Is.EqualTo("belt_straw"));
            Assert.That(World.MaterialName(straw.Material), Is.EqualTo("straw"));
            Assert.That(soil.Center.Y + soil.HalfExtents.Y, Is.EqualTo(-0.125).Within(Exact));
            Assert.That(straw.Center.Y + straw.HalfExtents.Y, Is.EqualTo(0).Within(Exact));

            var samples = new GroundSample[1];
            World.SampleGround(new[] { new XZ(straw.Center.X, straw.Center.Z) }, samples);
            Assert.That(samples[0].Flags, Is.EqualTo(GroundFlags.Hole), "the plane is absent over the patch");

            // A foot 1 mm into the straw strip's middle, with a margin that reaches the soil 0.124 m below.
            const double r = 0.005;
            var at = new Double3(straw.Center.X, r - 0.001, straw.Center.Z);
            StaticContact[] c = Contacts(World, new Capsule(at, at, r), new Capsule(at, at, r), 0.2);
            AssertCanonicalAndOnce(c);
            Assert.That(c.Length, Is.EqualTo(2), "the soil and the straw, each once, and no plane");
            Assert.That(c[0].Object, Is.EqualTo(patch));
            Assert.That(c[0].Shape, Is.EqualTo(0));
            Assert.That(c[0].Surface, Is.EqualTo(soil.Surface));
            Assert.That(c[0].Distance, Is.EqualTo(0.124).Within(Exact));
            Assert.That(c[1].Shape, Is.EqualTo(1));
            Assert.That(c[1].Material, Is.EqualTo(straw.Material));
            Assert.That(c[1].Distance, Is.EqualTo(-0.001).Within(Exact));
            Assert.That(c[1].Time, Is.EqualTo(1));
        }

        [Test]
        public void CapsuleStraddlingABandEdge_GetsThePlaneOnlyWhereItsContactPointLiesOutside()
        {
            int shaft = Object("Z04_cellar_shaft");
            Assert.That(World.Geometry(shaft, 1, out ShapeGeometry wall), Is.True);
            // Z04's band (placements.json ground.holes "shaft"): a 1.5 m band along x = −162.75, from z −59.25 to −61.25.
            // Its east wall (shape 1) runs from x −162.3 to the band's edge at −162.0, its top 5 mm above the plane.
            Assert.That(wall.Center.X + wall.HalfExtents.X, Is.EqualTo(-162.0).Within(Exact));
            const double r = 0.005, z = -60.25;
            var inside = new Double3(-162.1, 0.010, z);  // over the wall, inside the band
            var outside = new Double3(-161.8, 0.008, z); // over the plane, 3 mm above it
            var samples = new GroundSample[2];
            World.SampleGround(new[] { new XZ(inside.X, z), new XZ(outside.X, z) }, samples);
            Assert.That(samples[0].Flags, Is.EqualTo(GroundFlags.Hole));
            Assert.That(samples[1].Flags, Is.EqualTo(GroundFlags.None));

            // The lower core end lies outside: the plane answers there, at its foot.
            var capsule = new Capsule(inside, outside, r);
            StaticContact[] c = Contacts(World, capsule, capsule);
            AssertCanonicalAndOnce(c);
            Assert.That(c[0].Object, Is.EqualTo(WorldIds.Ground));
            AssertNear(c[0].Point, new Double3(outside.X, 0, z), Exact, "the plane's point");
            Assert.That(c[0].Distance, Is.EqualTo(0.003).Within(Exact));
            Assert.That(Array.Exists(c, k => k.Object == shaft && k.Shape == 1), Is.True, "the wall carries the inside end");

            // The lower core end lies inside the band: the plane is absent, and the wall is the ground there.
            capsule = new Capsule(inside - new Double3(0, 0.003, 0), outside, r);
            c = Contacts(World, capsule, capsule);
            AssertCanonicalAndOnce(c);
            Assert.That(Array.Exists(c, k => k.Object == WorldIds.Ground), Is.False);
            Assert.That(Array.Exists(c, k => k.Object == shaft && k.Shape == 1 && k.Distance < 0), Is.True);
        }

        // ---- Wires ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// Spheres of r = 7.5 mm (K§10.4's 15 mm sphere) moved 60 mm in one step across random points of a wire's chain,
        /// perpendicular to the segment there, passing within 0.9 (R + r) of its axis: every one is one contact with that
        /// wire with Time below 1, its Distance about 0, and WireParam the length along the chain to the crossing.
        /// </summary>
        private static void AssertFastSpheresCaught(MapWorld world, int obj, int shape, int seed, int count)
        {
            ReadOnlySpan<Double3> points = world.WirePoints(obj, shape);
            Assert.That(points.Length, Is.GreaterThan(2));
            Assert.That(world.Geometry(obj, shape, out ShapeGeometry g), Is.True);
            Assert.That(g.Kind, Is.EqualTo(ShapeKind.Wire));
            var along = new double[points.Length];
            for (int k = 1; k < points.Length; k++)
                along[k] = along[k - 1] + (points[k] - points[k - 1]).Length();
            double total = along[points.Length - 1];

            const double r = 0.0075, step = 0.06;
            double reach = g.Radius + r;
            var rng = new Random(seed);
            for (int trial = 0; trial < count; trial++)
            {
                int k = rng.Next(points.Length - 1);
                double f = 0.1 + 0.8 * rng.NextDouble();
                Double3 a = points[k], b = points[k + 1], u = (b - a) * (1 / (b - a).Length());
                Double3 c = a + (b - a) * f;
                // Two unit vectors across the segment, then a random direction and offset in that plane.
                Double3 p1 = Double3.Cross(u, Math.Abs(u.Y) < 0.9 ? Up : new Double3(1, 0, 0));
                p1 = p1 * (1 / p1.Length());
                Double3 p2 = Double3.Cross(u, p1);
                double angle = 2 * Math.PI * rng.NextDouble();
                Double3 d = p1 * Math.Cos(angle) + p2 * Math.Sin(angle), side = Double3.Cross(u, d);
                Double3 pass = c + side * ((2 * rng.NextDouble() - 1) * 0.9 * reach);
                Double3 from = pass - d * (step / 2), to = pass + d * (step / 2);
                StaticContact[] hits = Contacts(world, new Capsule(from, from, r), new Capsule(to, to, r));
                StaticContact[] mine = Array.FindAll(hits, h => h.Object == obj && h.Shape == shape);
                Assert.That(mine.Length, Is.EqualTo(1), $"trial {trial}: the sphere crossed the wire unseen");
                StaticContact w = mine[0];
                Assert.That(w.Time, Is.GreaterThan(0).And.LessThan(1), $"trial {trial}: Time");
                Assert.That(w.Distance, Is.EqualTo(0).Within(Advance), $"trial {trial}: Distance at the first touch");
                Assert.That(w.Material, Is.EqualTo(g.Material));
                Assert.That(w.WireParam, Is.EqualTo((along[k] + (along[k + 1] - along[k]) * f) / total).Within(1e-9),
                    $"trial {trial}: WireParam");
            }
        }

        [Test]
        public void FastSmallSpheres_AreAlwaysCaughtByAnL03Conductor()
        {
            int line = Object("L03_medium");
            Assert.That(World.MaterialName(World.Geometry(line, 0, out ShapeGeometry g) ? g.Material : (ushort)0), Is.EqualTo("cable"));
            Assert.That(g.Radius, Is.EqualTo(0.0045), "half the 9 mm conductor");
            for (int shape = 0; shape < 4; shape++)
                AssertFastSpheresCaught(World, line, shape, 450 + shape, 250);
        }

        /// <summary>A map of one 5 mm wire, 10 m along x turned by 30°, 3 m up with 0.2 m of sag, as placements and metadata.</summary>
        private static MapWorld FiveMillimetreWire()
        {
            var points = new List<string>();
            for (int k = 0; k <= 20; k++)
            {
                double x = 0.5 * k, s = x / 10;
                points.Add(FormattableString.Invariant($"[{x}, {3 - 0.8 * s * (1 - s):0.######}, 0.0]"));
            }
            string meta = "{\"frame\": \"unity-lh-yup\", \"units\": \"m\", \"collision\": [], \"wires\": [{\"diameter_m\": 0.005, "
                + "\"sag_m\": 0.2, \"material\": \"cable\", \"points\": [" + string.Join(", ", points) + "]}]}";
            string placements = "{\"format\": \"fpv450-placements\", \"format_version\": 1, \"map\": \"wire\", \"frame\": \"unity-lh-yup\", "
                + "\"extent_m\": [-16.0, -16.0, 16.0, 16.0], \"ground\": {\"plane\": {\"y_m\": 0.0, \"surface\": \"meadow_sod\"}, "
                + "\"patches\": [], \"holes\": []}, \"instances\": [{\"id\": 0, \"class\": \"lines\", \"asset\": \"W\", \"variant\": \"W_5mm\", "
                + "\"position_m\": [-3.0, 0.0, 1.0], \"yaw_deg\": 30.0}]}";
            return new MapWorld(placements, path => path == "lines/W/W_5mm.assetmeta.json" ? meta : null, new[] { "cable" });
        }

        [Test]
        public void FastSmallSpheres_AreAlwaysCaughtByAFiveMillimetreWire()
        {
            MapWorld world = FiveMillimetreWire();
            Assert.That(world.Geometry(0, 0, out ShapeGeometry g), Is.True);
            Assert.That(g.Radius, Is.EqualTo(0.0025));
            AssertFastSpheresCaught(world, 0, 0, 5, 1000);
        }

        [Test]
        public void WireDistance_IsTheTrueSignedOneDeepInside_AndGeometryGivesItsSpan()
        {
            int line = Object("L03_medium");
            ReadOnlySpan<Double3> points = World.WirePoints(line, 0);
            Assert.That(World.Geometry(line, 0, out ShapeGeometry g), Is.True);
            AssertNear(g.SpanA, points[0], 0, "SpanA");
            AssertNear(g.SpanB, points[points.Length - 1], 0, "SpanB");
            AssertNear(g.Center, (points[0] + points[points.Length - 1]) * 0.5, Exact, "the chord's midpoint");
            Assert.That(g.Sag, Is.EqualTo(0.8));
            Assert.That(World.WirePoints(line, 4).Length, Is.EqualTo(0), "no fifth wire");
            Assert.That(World.WirePoints(Object("L01_straight"), 0).Length, Is.EqualTo(0), "a capsule is not a wire");

            // A resting 10 mm sphere 1 mm off the axis at a quarter of segment 10, and one on the axis itself.
            Double3 a = points[10], b = points[11], c = a + (b - a) * 0.25;
            Double3 off = Double3.Cross(b - a, new Double3(0, 0, 1));
            off = off * (0.001 / off.Length());
            foreach ((Double3 at, double depth) in new[] { (c + off, 0.001), (c, 0.0) })
            {
                StaticContact[] hits = Contacts(World, new Capsule(at, at, 0.01), new Capsule(at, at, 0.01));
                StaticContact[] w = Array.FindAll(hits, h => h.Object == line && h.Shape == 0);
                Assert.That(w.Length, Is.EqualTo(1));
                Assert.That(w[0].Distance, Is.EqualTo(depth - 0.0045 - 0.01).Within(Exact), "the signed distance");
                Assert.That(w[0].Time, Is.EqualTo(1));
                Assert.That(w[0].Normal.Length(), Is.EqualTo(1).Within(Exact));
            }
        }

        [Test]
        public void RayRecastFromInsideAConductor_GoesOnToTheGround()
        {
            int line = Object("L03_medium");
            ReadOnlySpan<Double3> points = World.WirePoints(line, 0);
            Double3 low = points[40]; // mid-span, its lowest point
            var hits = new RayHit[1];
            var down = new Double3(0, -1, 0);
            World.Raycast(new[] { new Ray(new Double3(low.X, 10, low.Z), down) }, 20, hits);
            Assert.That(hits[0].Object, Is.EqualTo(line));
            Assert.That(hits[0].Shape, Is.EqualTo(0));
            Assert.That(World.MaterialName(hits[0].Material), Is.EqualTo("cable"));
            Assert.That(hits[0].Distance, Is.EqualTo(10 - low.Y - 0.0045).Within(1e-6), "the conductor's top");

            // Re-cast from 1 mm inside the conductor: it does not hit the wire it starts in, and goes on to the field.
            Double3 inside = hits[0].Point + down * 0.001;
            World.Raycast(new[] { new Ray(inside, down) }, 20, hits);
            Assert.That(hits[0].Object, Is.EqualTo(WorldIds.Ground));
            Assert.That(World.SurfaceName(hits[0].Surface), Is.EqualTo("belt_straw"));
            Assert.That(hits[0].Distance, Is.EqualTo(inside.Y).Within(Exact));

            // A ray from inside one segment's middle along the conductor does not enter the next segment's capsule: the
            // whole wire is skipped, and nothing else lies within 5 m along the span.
            Double3 a = points[10], b = points[11];
            World.Raycast(new[] { new Ray((a + b) * 0.5, b - a) }, 5, hits);
            Assert.That(hits[0].Distance, Is.EqualTo(double.PositiveInfinity));
        }

        // ---- The launch stand -----------------------------------------------------------------------------------------

        [Test]
        public void LaunchStand_IsAddedAtTheFieldStart_OneContactPerPost_AndResetRemovesIt()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            StartPoint start = World.StartPoints[1];
            Double3 foot = start.Position;
            RuntimeObject stand = LaunchStand.Build(spec, foot, start.HeadingDeg * Math.PI / 180, World.MaterialId(spec.Stand.Material));
            try
            {
                int id = World.AddObject(stand.Name, stand.Shapes);
                Assert.That(id, Is.EqualTo(World.PlacedObjects));
                Assert.That(World.ObjectName(id), Is.EqualTo(LaunchStand.Name));
                for (int post = 0; post < 4; post++)
                {
                    Assert.That(World.Geometry(id, post, out ShapeGeometry g), Is.True);
                    // A 5 mm sphere resting 1 mm into each post's top.
                    var at = new Double3(g.Center.X, g.Center.Y + g.HalfExtents.Y + 0.004, g.Center.Z);
                    StaticContact[] c = Contacts(World, new Capsule(at, at, 0.005), new Capsule(at, at, 0.005));
                    StaticContact[] mine = Array.FindAll(c, k => k.Object == id);
                    Assert.That(mine.Length, Is.EqualTo(1), $"post {post}");
                    Assert.That(mine[0].Shape, Is.EqualTo(post));
                    Assert.That(mine[0].Distance, Is.EqualTo(-0.001).Within(Exact));
                    Assert.That(World.MaterialName(mine[0].Material), Is.EqualTo("steel"));
                }

                World.ResetRuntimeObjects();
                Assert.That(World.ObjectName(id), Is.Null);
                Assert.That(World.Geometry(id, 0, out _), Is.False);
                Assert.That(World.AddObject(stand.Name, stand.Shapes), Is.EqualTo(id), "the same id after a reset");
            }
            finally
            {
                World.ResetRuntimeObjects();
            }
        }

        // ---- Identity and pose parity ---------------------------------------------------------------------------------

        private static Double3 Rotate(double x, double y, double z, double w, Double3 v)
        {
            // v' = v + 2w (u × v) + 2 u × (u × v), with u = (x, y, z) (Unity's quaternion rotation).
            var u = new Double3(x, y, z);
            Double3 t = Double3.Cross(u, v) * 2;
            return v + t * w + Double3.Cross(u, t);
        }

        private static Double3 Vec(object n)
        {
            var v = (List<object>)n;
            return new Double3((double)v[0], (double)v[1], (double)v[2]);
        }

        private static Dictionary<string, object> Obj(object n) => (Dictionary<string, object>)n;

        private static List<object> List(object n) => (List<object>)n;

        [Test]
        public void PoseParity_EveryShapeAndWirePointLiesWithinAMillimetreOfItsRowAndMetadata()
        {
            // Unity places a row at Quaternion.Euler(0, yaw, 0) and each collider child at its metadata pose below it
            // (TestRangeScene, CatalogImporter.EnsureColliders); recomputed here with System.Math, independently of the loader.
            List<object> rows = List(Obj(TestJson.Parse(Placements()))["instances"]);
            var metas = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            int shapes = 0, wirePoints = 0;
            double worst = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                Dictionary<string, object> row = Obj(rows[i]);
                string path = row["class"] + "/" + row["asset"] + "/" + row["variant"] + ".assetmeta.json";
                if (!metas.TryGetValue(path, out Dictionary<string, object> meta))
                    metas.Add(path, meta = Obj(TestJson.Parse(ReadModel(path))));
                double half = (double)row["yaw_deg"] * Math.PI / 360;
                double ys = Math.Sin(half), yc = Math.Cos(half);
                Double3 position = Vec(row["position_m"]);
                List<object> collision = List(meta["collision"]);
                for (int s = 0; s < collision.Count; s++, shapes++)
                {
                    Dictionary<string, object> c = Obj(collision[s]);
                    Assert.That(World.Geometry(i, s, out ShapeGeometry g), Is.True);
                    Double3 expected = position + Rotate(0, ys, 0, yc, Vec(c["center"]));
                    worst = Math.Max(worst, (g.Center - expected).Length());
                    List<object> q = c.TryGetValue("rotation", out object rotation) ? List(rotation) : null;
                    double qx = q == null ? 0 : (double)q[0], qy = q == null ? 0 : (double)q[1];
                    double qz = q == null ? 0 : (double)q[2], qw = q == null ? 1 : (double)q[3];
                    double norm = Math.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
                    // The row's yaw times the shape's rotation: (0, ys, 0, yc) * (qx, qy, qz, qw), normalised.
                    double rx = (yc * qx + ys * qz) / norm, ry = (yc * qy + ys * qw) / norm, rz = (yc * qz - ys * qx) / norm, rw = (yc * qw - ys * qy) / norm;
                    Double3 axisY = Rotate(rx, ry, rz, rw, Up);
                    Assert.That((g.AxisY - axisY).Length(), Is.LessThan(1e-6), $"row {i} shape {s}: its axis");
                    switch ((string)c["shape"])
                    {
                        case "box":
                            AssertNear(g.HalfExtents, Vec(c["size"]) * 0.5, 0, $"row {i} shape {s}: half size");
                            break;
                        case "capsule":
                            Assert.That(g.Radius, Is.EqualTo((double)c["radius"]));
                            Assert.That(g.HalfLength, Is.EqualTo(Math.Max(0, (double)c["height"] / 2 - g.Radius)).Within(Exact));
                            break;
                        case "cylinder":
                            Assert.That(g.Radius, Is.EqualTo((double)c["radius"]));
                            Assert.That(g.HalfLength, Is.EqualTo((double)c["height"] / 2).Within(Exact));
                            break;
                        default:
                            Assert.That(g.Radius, Is.EqualTo((double)c["radius"]));
                            break;
                    }
                }
                List<object> wires = List(meta["wires"]);
                for (int w = 0; w < wires.Count; w++)
                {
                    ReadOnlySpan<Double3> chain = World.WirePoints(i, collision.Count + w);
                    List<object> local = List(Obj(wires[w])["points"]);
                    Assert.That(chain.Length, Is.EqualTo(local.Count));
                    for (int k = 0; k < local.Count; k++, wirePoints++)
                        worst = Math.Max(worst, (chain[k] - (position + Rotate(0, ys, 0, yc, Vec(local[k])))).Length());
                }
                Assert.That(World.Geometry(i, collision.Count + wires.Count, out _), Is.False, $"row {i}: one shape too many");
            }
            Assert.That(worst, Is.LessThan(0.001), "the worst offset, m");
            TestContext.WriteLine($"{rows.Count} rows, {shapes} shapes, {wirePoints} wire points: worst offset {worst:E2} m");
        }

        [Test]
        public void ContentHash_MovesWithAMillimetreEditOfARowOrAShape()
        {
            string placements = Placements();
            string[] materials = Materials();
            var same = new MapWorld(placements, ReadModel, materials);
            Assert.That(same.ContentHash, Is.EqualTo(World.ContentHash), "the same files hash the same");
            Assert.That(new MapWorld(placements.Replace("\n", "\r\n"), ReadModel, materials).ContentHash, Is.EqualTo(World.ContentHash),
                "CRLF reads as LF");

            // L01_straight's row (id 17) moved 1 mm east.
            const string row = "\"variant\": \"L01_straight\", \"class\": \"lines\", \"position_m\": [-160.0, 0.0, -34.0]";
            int at = placements.IndexOf(row, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0));
            string moved = placements.Substring(0, at) + row.Replace("-160.0,", "-159.999,") + placements.Substring(at + row.Length);
            var edited = new MapWorld(moved, ReadModel, materials);
            Assert.That(edited.ContentHash, Is.Not.EqualTo(World.ContentHash));
            Assert.That(edited.Geometry(17, 0, out ShapeGeometry g), Is.True);
            Assert.That(World.Geometry(17, 0, out ShapeGeometry g0), Is.True);
            Assert.That(g.Center.X - g0.Center.X, Is.EqualTo(0.001).Within(Exact));

            // L01_straight's foot capsule raised 1 mm in its metadata (its first collision centre's y, 1.25).
            string Raised(string path)
            {
                string text = ReadModel(path);
                if (!path.EndsWith("L01_straight.assetmeta.json", StringComparison.Ordinal))
                    return text;
                int y = text.IndexOf("1.25,", text.IndexOf("\"collision\"", StringComparison.Ordinal), StringComparison.Ordinal);
                return text.Substring(0, y) + "1.251," + text.Substring(y + 5);
            }
            var raised = new MapWorld(placements, Raised, materials);
            Assert.That(raised.Geometry(17, 0, out ShapeGeometry up), Is.True);
            Assert.That(up.Center.Y - g0.Center.Y, Is.EqualTo(0.001).Within(Exact));
            Assert.That(raised.ContentHash, Is.Not.EqualTo(World.ContentHash));
            Assert.That(raised.ContentHash, Is.Not.EqualTo(edited.ContentHash));
        }

        // ---- Determinism ----------------------------------------------------------------------------------------------

        /// <summary>A fixed set of queries around the test range's busiest places, and one FNV-1a hash of every result bit.</summary>
        private static ulong RunQueries(IWorldQuery world, int seed, int count)
        {
            Double3[] spots =
            {
                new Double3(-122, 2, -57), new Double3(59.5, 0, -14.6), new Double3(-134, 1, -13.4),
                new Double3(-180, 6.5, -34), new Double3(-162.75, 0, -60.25), new Double3(-160, 1.2, -34),
            };
            var rng = new Random(seed);
            double Next(double half) => (2 * rng.NextDouble() - 1) * half;
            ulong hash = WorldHash.Offset;
            void Add(double v)
            {
                ulong bits = (ulong)BitConverter.DoubleToInt64Bits(v);
                for (int k = 0; k < 8; k++)
                    hash = (hash ^ (byte)(bits >> 8 * k)) * 0x100000001B3UL;
            }
            var contacts = new StaticContact[32];
            var sample = new GroundSample[1];
            var hit = new RayHit[1];
            for (int i = 0; i < count; i++)
            {
                Double3 c = spots[i % spots.Length], a = c + new Double3(Next(3), Next(2), Next(3));
                Double3 b = i % 3 == 0 ? a : a + new Double3(Next(0.15), Next(0.15), Next(0.15)), move = new Double3(Next(0.3), Next(0.3), Next(0.3));
                int n = world.StaticContacts(new Capsule(a, b, 0.005 + 0.05 * rng.NextDouble()), new Capsule(a + move, b + move, 0.005 + 0.05 * rng.NextDouble()),
                    Margin, contacts);
                Add(n);
                for (int k = 0; k < Math.Min(n, contacts.Length); k++)
                {
                    StaticContact s = contacts[k];
                    Add(s.Point.X); Add(s.Point.Y); Add(s.Point.Z); Add(s.Normal.X); Add(s.Normal.Y); Add(s.Normal.Z);
                    Add(s.Distance); Add(s.Object); Add(s.Shape); Add(s.Material); Add(s.Surface); Add(s.WireParam); Add(s.Time);
                }
                world.SampleGround(new[] { new XZ(a.X, a.Z) }, sample);
                Add(sample[0].Height); Add(sample[0].Normal.Y); Add(sample[0].Surface); Add((int)sample[0].Flags);
                world.Raycast(new[] { new Ray(a, new Double3(Next(1), Next(1) - 0.5, Next(1))) }, 5, hit);
                Add(hit[0].Distance); Add(hit[0].Point.X); Add(hit[0].Point.Y); Add(hit[0].Point.Z); Add(hit[0].Normal.X);
                Add(hit[0].Object); Add(hit[0].Shape); Add(hit[0].Material); Add(hit[0].Surface);
            }
            return hash;
        }

        [Test]
        public void Concurrency_FourThreadsOnOneWorldGetTheBitsOfOne()
        {
            MapWorld world = World;
            const int count = 3000;
            ulong alone = RunQueries(world, 7, count);
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
                            results[me] = RunQueries(world, 7, count);
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
        public void BatchedEqualsSingle_ForSamplesAndRays()
        {
            var rng = new Random(11);
            var points = new XZ[2000];
            var rays = new Ray[2000];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new XZ(-240 + 480 * rng.NextDouble(), -240 + 480 * rng.NextDouble());
                if (i % 2 == 0)
                    points[i] = new XZ(44 + 35 * rng.NextDouble(), -21 + 13.5 * rng.NextDouble()); // E01 and its holes
                rays[i] = new Ray(new Double3(points[i].X, 0.2 + 8 * rng.NextDouble(), points[i].Z),
                    new Double3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.8, rng.NextDouble() - 0.5));
            }
            var samples = new GroundSample[points.Length];
            var hits = new RayHit[rays.Length];
            World.SampleGround(points, samples);
            World.Raycast(rays, 10, hits);
            var one = new GroundSample[1];
            var hit = new RayHit[1];
            int holes = 0;
            for (int i = 0; i < points.Length; i++)
            {
                World.SampleGround(points.AsSpan(i, 1), one);
                Assert.That(one[0].Height.Equals(samples[i].Height) && one[0].Flags == samples[i].Flags && one[0].Surface == samples[i].Surface, Is.True);
                holes += samples[i].Flags == GroundFlags.Hole ? 1 : 0;
                World.Raycast(rays.AsSpan(i, 1), 10, hit);
                Assert.That(hit[0].Distance.Equals(hits[i].Distance) && hit[0].Object == hits[i].Object && hit[0].Shape == hits[i].Shape
                    && hit[0].Point.X.Equals(hits[i].Point.X) && hit[0].Normal.Y.Equals(hits[i].Normal.Y), Is.True, $"ray {i}");
            }
            Assert.That(holes, Is.GreaterThan(500), "E01's patch samples are holes");
        }
    }

    /// <summary>A plain JSON reader for the tests (objects, arrays, strings, numbers by double.Parse, true/false/null), so
    /// the pose parity is read independently of the loader's DetJson.</summary>
    internal static class TestJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            object value = Value(text, ref i);
            return value;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var o = new Dictionary<string, object>(StringComparer.Ordinal);
                i++;
                for (Skip(s, ref i); s[i] != '}'; Skip(s, ref i))
                {
                    string key = (string)Value(s, ref i);
                    Skip(s, ref i);
                    i++; // ':'
                    o[key] = Value(s, ref i);
                    Skip(s, ref i);
                    if (s[i] == ',')
                        i++;
                }
                i++;
                return o;
            }
            if (c == '[')
            {
                var a = new List<object>();
                i++;
                for (Skip(s, ref i); s[i] != ']'; Skip(s, ref i))
                {
                    a.Add(Value(s, ref i));
                    Skip(s, ref i);
                    if (s[i] == ',')
                        i++;
                }
                i++;
                return a;
            }
            if (c == '"')
            {
                var b = new System.Text.StringBuilder();
                for (i++; s[i] != '"'; i++)
                {
                    if (s[i] == '\\')
                    {
                        i++;
                        b.Append(s[i] == 'u' ? (char)Convert.ToInt32(s.Substring(i + 1, 4), 16) : s[i] == 'n' ? '\n' : s[i]);
                        if (s[i] == 'u')
                            i += 4;
                    }
                    else
                        b.Append(s[i]);
                }
                i++;
                return b.ToString();
            }
            int start = i;
            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.'))
                i++;
            string word = s.Substring(start, i - start);
            if (word == "true" || word == "false")
                return word == "true";
            if (word == "null")
                return null;
            return double.Parse(word, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
