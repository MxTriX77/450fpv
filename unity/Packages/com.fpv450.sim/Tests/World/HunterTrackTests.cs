using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The hunter-09 map through the sim's own loaded world (w20-hunter-track 1.4): the ground under the feet and the gates, the
    // objects fixed to the houses, and every gate's and house opening's gap. A gap's polygon is its variant's metadata placed
    // at the row's pose, recomputed here; "the gap query" is that record together with rays through it, since IWorldQuery has
    // no gap call of its own.
    public sealed class HunterTrackTests
    {
        private const double FootTolerance = 0.05, FixedTolerance = 0.05; // the spec
        private const double Rounding = 0.001; // an opening's lower edge may rest on the ground, to the sample's rounding
        private const double Through = 0.30;   // past a wall's 0.18 m and a gate's depth, each way along the opening's normal

        private static readonly string[] FootKinds = { "T01_pylon_3m", "T01_cone_light", "T01_light_gate", "T01_tent", "T01_notice_board" };
        private static readonly int[] FootFrames = { 329, 330, 331 };
        private static readonly string[] GateKinds = { "T01_frame_8x10", "T01_frame_square_a", "T01_frame_square_b", "T01_light_gate" };
        private static readonly int[] OnNorthHouse = { 410, 411, 412, 413, 387, 403, 395, 419, 420, 421, 384, 385, 418, 415, 417 };
        private static readonly int[] OnBatten = { 405, 406, 407, 408 };
        private static readonly Dictionary<string, int[]> Markers = new Dictionary<string, int[]>
        {
            ["T02_north"] = new[] { 49, 50, 48, 51, 416, 47, 52, 386, 388, 394, 396 },
            ["T02_north_east"] = new[] { 41, 42, 40, 46, 39, 43 },
            ["T02_south"] = new[] { 29, 30, 28, 31, 27, 32 },
        };

        private sealed class Row
        {
            public int Id, LayoutId = -1;
            public string Variant;
            public Double3 Position;
            public Quat Rotation;
            public Double3 Place(Double3 local) => Position + Rotation.Rotate(local);
        }

        private sealed class Gap
        {
            public string Id;
            public Double3[] Corners;
            public Double3 Normal, Centre;
        }

        private static MapWorld _world;
        private static Row[] _rows;

        private static MapWorld World => _world ?? (_world = MapWorld.Load(MapWorldTests.Root, "hunter-09"));

        private static Row[] Rows => _rows ?? (_rows = ReadRows());

        /// <summary>The map cannot load before task 1.3c builds the houses' metadata: until then these tests are inconclusive.</summary>
        [SetUp]
        public void TheHousesAreBuilt()
        {
            foreach (string variant in Markers.Keys)
                Assume.That(File.Exists(Path.Combine(MapWorldTests.Root, MapWorld.ModelsRoot, "track/T02", variant + ".assetmeta.json")),
                    variant + " is not built yet (w20-hunter-track 1.3c)");
        }

        private static Dictionary<string, object> Obj(object n) => (Dictionary<string, object>)n;

        private static List<object> List(object n) => (List<object>)n;

        private static Double3 Vec(object n)
        {
            List<object> v = List(n);
            return new Double3((double)v[0], (double)v[1], (double)v[2]);
        }

        private static Row[] ReadRows()
        {
            string text = File.ReadAllText(Path.Combine(MapWorldTests.Root, "maps/hunter-09/placements.json"));
            return List(Obj(TestJson.Parse(text))["placements"]).Select(n =>
            {
                Dictionary<string, object> r = Obj(n);
                List<object> q = List(r["rotation_xyzw"]);
                return new Row
                {
                    Id = (int)(double)r["id"],
                    LayoutId = r.TryGetValue("layout_id", out object layout) ? (int)(double)layout : -1,
                    Variant = (string)r["variant"],
                    Position = Vec(r["position_m"]),
                    Rotation = new Quat((double)q[0], (double)q[1], (double)q[2], (double)q[3]).Normalized(),
                };
            }).ToArray();
        }

        private static Gap[] Gaps(Row row)
        {
            string asset = row.Variant.Substring(0, 3);
            var meta = Obj(TestJson.Parse(MapWorldTests.ReadModel("track/" + asset + "/" + row.Variant + ".assetmeta.json")));
            return List(meta["gaps"]).Select(n =>
            {
                Dictionary<string, object> g = Obj(n);
                Double3[] corners = List(g["polygon"]).Select(p => row.Place(Vec(p))).ToArray();
                Double3 sum = default;
                foreach (Double3 c in corners)
                    sum = sum + c;
                return new Gap { Id = (string)g["id"], Corners = corners, Normal = row.Rotation.Rotate(Vec(g["normal"])), Centre = sum * (1.0 / corners.Length) };
            }).ToArray();
        }

        private static double GroundAt(double x, double z)
        {
            var results = new GroundSample[1];
            World.SampleGround(new[] { new XZ(x, z) }, results);
            return results[0].Height;
        }

        /// <summary>The first hit of the ray from `from` along the unit `direction` within `distance`, or null.</summary>
        private static RayHit? Cast(Double3 from, Double3 direction, double distance)
        {
            var hits = new RayHit[1];
            World.Raycast(new[] { new Ray(from, direction) }, distance, hits);
            return double.IsInfinity(hits[0].Distance) ? (RayHit?)null : hits[0];
        }

        private static void AssertClearThrough(Gap gap, string what)
        {
            Double3 from = gap.Centre - gap.Normal * Through;
            RayHit? hit = Cast(from, gap.Normal, 2 * Through);
            Assert.That(hit, Is.Null, what + ": a ray through the opening's centre hit " + (hit.HasValue ? World.ObjectName(hit.Value.Object) + " at " + hit.Value.Distance : ""));
        }

        [Test]
        public void EveryDistinctDrawnRowAndTheHousesAreLoaded()
        {
            Assert.That(World.MapId, Is.EqualTo("hunter-09"));
            Assert.That(Rows.Count(r => r.LayoutId >= 0), Is.EqualTo(272));
            Assert.That(Rows.Where(r => r.LayoutId < 0).Select(r => r.Variant), Is.EqualTo(new[] { "T02_north", "T02_north_east", "T02_south" }));
            foreach (Row r in Rows)
                Assert.That(World.ObjectName(r.Id), Is.EqualTo(r.Variant));
            Assert.That(World.ObjectName(Rows.Length), Is.Null, "nothing placed beyond the rows");
        }

        [Test]
        public void TheUprightFeetMeetTheGround()
        {
            Row[] feet = Rows.Where(r => FootKinds.Contains(r.Variant) || FootFrames.Contains(r.LayoutId)).ToArray();
            Assert.That(feet.Length, Is.EqualTo(32));
            foreach (Row r in feet)
                Assert.That(Math.Abs(r.Position.Y - GroundAt(r.Position.X, r.Position.Z)), Is.LessThanOrEqualTo(FootTolerance), r.Variant + " " + r.LayoutId);
        }

        [Test]
        public void EveryGateIsOpenAboveTheGroundAndItsFrameAnswers()
        {
            Row[] gates = Rows.Where(r => GateKinds.Contains(r.Variant)).ToArray();
            Assert.That(gates.Length, Is.EqualTo(10));
            foreach (Row r in gates)
            {
                string what = r.Variant + " " + r.LayoutId;
                Gap gap = Gaps(r).Single();
                for (int e = 0; e < gap.Corners.Length; e++)
                {
                    Double3 a = gap.Corners[e], b = gap.Corners[(e + 1) % gap.Corners.Length];
                    for (int i = 0; i <= 20; i++)
                    {
                        Double3 p = a + (b - a) * (i / 20.0);
                        Assert.That(p.Y, Is.GreaterThanOrEqualTo(GroundAt(p.X, p.Z) - Rounding), what + ": the opening meets the ground");
                    }
                }
                AssertClearThrough(gap, what);

                // A ray parallel to the axis, 100 mm outside the opening's side: the frame's member.
                Double3 side = gap.Corners[1] - gap.Corners[0];
                double width = side.Length();
                side = side * (1 / width);
                Double3 onMember = gap.Centre + side * (width / 2 + 0.1);
                RayHit? hit = Cast(onMember - gap.Normal * 1.0, gap.Normal, 2.0);
                Assert.That(hit.HasValue && hit.Value.Object == r.Id, what + ": a ray at the frame's member misses it");
            }
        }

        [Test]
        public void EveryMarkedHouseOpeningIsAGapWithNothingInIt()
        {
            int openings = 0;
            foreach (KeyValuePair<string, int[]> house in Markers)
            {
                Row row = Rows.Single(r => r.Variant == house.Key);
                Gap[] gaps = Gaps(row);
                foreach (int marker in house.Value)
                {
                    string suffix = "_" + marker;
                    Gap gap = gaps.Single(g => g.Id.EndsWith(suffix, StringComparison.Ordinal));
                    AssertClearThrough(gap, house.Key + " " + gap.Id);
                    foreach (Double3 c in gap.Corners)
                        Assert.That(c.Y, Is.GreaterThanOrEqualTo(GroundAt(c.X, c.Z) - 0.01), house.Key + " " + gap.Id + ": the opening meets the ground");
                    openings++;
                }
            }
            Assert.That(openings, Is.EqualTo(23));
        }

        private static StaticContact[] ContactsOn(Double3 point, int obj)
        {
            var sphere = new Capsule(point, point, 1e-4);
            var results = new StaticContact[128];
            int n = World.StaticContacts(sphere, sphere, FixedTolerance, results);
            Assert.That(n, Is.LessThanOrEqualTo(results.Length));
            return results.Take(n).Where(c => c.Object == obj).ToArray();
        }

        [Test]
        public void ObjectsFixedToTheNorthHouseStayFixed()
        {
            int house = Rows.Single(r => r.Variant == "T02_north").Id;
            foreach (int id in OnNorthHouse)
                Assert.That(ContactsOn(Rows.Single(r => r.LayoutId == id).Position, house), Is.Not.Empty, "object " + id + " is not on a house surface");
            foreach (int id in OnBatten)
            {
                StaticContact[] on = ContactsOn(Rows.Single(r => r.LayoutId == id).Position, house);
                Assert.That(on.Any(c => World.MaterialName(c.Material) == "timber"), "object " + id + " is not on the birch's batten");
            }
        }

        [Test]
        public void ATiltedRowKeepsItsWindVolumes()
        {
            var withWind = new HashSet<int>();
            for (int k = 0; k < World.WindVolumes.Length; k++)
                withWind.Add(World.WindVolumeObject(k));
            // Board 418 hangs wholly below its origin, inside the north house: a volume's heights start at its row's foot, so it drops out.
            foreach (Row r in Rows.Where(r => (r.Rotation.X != 0 || r.Rotation.Z != 0) && r.LayoutId != 418))
            {
                var meta = Obj(TestJson.Parse(MapWorldTests.ReadModel("track/" + r.Variant.Substring(0, 3) + "/" + r.Variant + ".assetmeta.json")));
                if (List(meta["wind_volumes"]).Count > 0)
                    Assert.That(withWind, Does.Contain(r.Id), r.Variant + " " + r.LayoutId);
            }

            // Block 392 lies on its side: 0.762 wide, 1.524 long along its heading, its centre where its collision box's is.
            Row block = Rows.Single(r => r.LayoutId == 392);
            int at = Enumerable.Range(0, World.WindVolumes.Length).Single(k => World.WindVolumeObject(k) == block.Id);
            WindVolume w = World.WindVolumes[at];
            Assert.That(World.Geometry(block.Id, 0, out ShapeGeometry box));
            Assert.That(w.HalfX, Is.EqualTo(0.381).Within(0.02));
            Assert.That(w.HalfZ, Is.EqualTo(0.762).Within(0.02));
            Assert.That(w.X, Is.EqualTo(box.Center.X).Within(0.02));
            Assert.That(w.Z, Is.EqualTo(box.Center.Z).Within(0.02));
        }

        [Test]
        public void TheStartIsTheRacesSpawnPoint()
        {
            StartPoint start = World.StartPoints.Single();
            Assert.That(start.Id, Is.EqualTo("spawn"));
            Assert.That(start.LegsOn && !start.Stand);
            Assert.That(start.HeadingDeg, Is.EqualTo(58.948).Within(0.001));
        }
    }
}
