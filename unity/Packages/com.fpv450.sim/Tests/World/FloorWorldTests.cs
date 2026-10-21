using System;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The floor stub behind IWorldQuery (p6-world-query-gallery task 1.1). Expected
    // values are analytic (derived); tolerances name what limits them.
    public sealed class FloorWorldTests
    {
        private const double Exact = 1e-12;       // closed forms: only rounding
        private const double Advance = 1e-5;      // conservative advancement stops within 1 µm of the surface
        private static readonly Double3 Up = new Double3(0, 1, 0);

        private static FloorWorld World(double height = 0) => new FloorWorld(height, "meadow_sod", "steel", "timber");

        private static ShapeGeometry Shape(ShapeKind kind, Double3 center, ushort material, double radius = 0,
            double halfLength = 0, Double3 halfExtents = default) => new ShapeGeometry
        {
            Kind = kind,
            Center = center,
            AxisX = new Double3(1, 0, 0),
            AxisY = Up,
            AxisZ = new Double3(0, 0, 1),
            HalfExtents = halfExtents,
            Radius = radius,
            HalfLength = halfLength,
            Material = material,
        };

        private static void AssertNear(Double3 actual, Double3 expected, double tolerance)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(tolerance), "x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tolerance), "y");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(tolerance), "z");
        }

        [Test]
        public void SampleGround_ReturnsTheFloor()
        {
            FloorWorld world = World(0.25);
            var points = new[] { new XZ(3, -7), new XZ(0, 0) };
            var results = new GroundSample[2];
            world.SampleGround(points, results);
            foreach (GroundSample g in results)
            {
                Assert.That(g.Height, Is.EqualTo(0.25));
                AssertNear(g.Normal, Up, 0);
                Assert.That(g.Flags, Is.EqualTo(GroundFlags.None));
                Assert.That(world.SurfaceName(g.Surface), Is.EqualTo("meadow_sod"));
            }
        }

        [Test]
        public void SweptCapsule_IntoTheFloor_LatchesTheFirstTouchWithTimeBelowOne()
        {
            FloorWorld world = World(0.25);
            // A level arm, r = 0.02, from 0.08 above the floor to 0.03 into it: it touches when its core is at 0.27.
            var before = new Capsule(new Double3(0, 0.35, 0), new Double3(0.2, 0.35, 0), 0.02);
            var after = new Capsule(new Double3(0, 0.24, 0), new Double3(0.2, 0.24, 0), 0.02);
            var contacts = new StaticContact[4];
            Assert.That(world.StaticContacts(before, after, 0.01, contacts), Is.EqualTo(1));
            StaticContact c = contacts[0];
            Assert.That(c.Time, Is.GreaterThan(0).And.LessThan(1));
            Assert.That(c.Time, Is.EqualTo(0.08 / 0.11).Within(Exact));
            Assert.That(c.Object, Is.EqualTo(WorldIds.Ground));
            Assert.That(c.Shape, Is.EqualTo(0));
            Assert.That(c.Material, Is.EqualTo(WorldIds.NoMaterial));
            Assert.That(world.SurfaceName(c.Surface), Is.EqualTo("meadow_sod"));
            AssertNear(c.Normal, Up, 0);
            AssertNear(c.Point, new Double3(0, 0.25, 0), Exact);
            Assert.That(c.Distance, Is.EqualTo(0).Within(1e-6));
            Assert.That(c.WireParam, Is.EqualTo(-1));

            // The next steps start in contact on the same side: ordinary contacts at the current pose.
            var deeper = new Capsule(new Double3(0, 0.23, 0), new Double3(0.2, 0.23, 0), 0.02);
            Assert.That(world.StaticContacts(after, deeper, 0.01, contacts), Is.EqualTo(1));
            Assert.That(contacts[0].Time, Is.EqualTo(1));
            Assert.That(contacts[0].Distance, Is.EqualTo(-0.04).Within(Exact));
            Assert.That(world.StaticContacts(deeper, deeper, 0.01, contacts), Is.EqualTo(1));
            Assert.That(contacts[0].Time, Is.EqualTo(1));
        }

        [Test]
        public void SweptCapsule_AboveTheFloor_OnlyWithinTheMargin()
        {
            FloorWorld world = World();
            var contacts = new StaticContact[4];
            var high = new Capsule(new Double3(0, 0.5, 0), new Double3(0, 0.6, 0), 0.05);
            var low = new Capsule(new Double3(0, 0.06, 0), new Double3(0, 0.16, 0), 0.05);
            Assert.That(world.StaticContacts(high, high, 0.02, contacts), Is.EqualTo(0));
            Assert.That(world.StaticContacts(high, low, 0.02, contacts), Is.EqualTo(1));
            Assert.That(contacts[0].Time, Is.EqualTo(1), "never touched: the current pose within the margin");
            Assert.That(contacts[0].Distance, Is.EqualTo(0.01).Within(Exact));
            Assert.That(world.StaticContacts(low, high, 0.02, contacts), Is.EqualTo(0), "moved clear");
        }

        [Test]
        public void SweptArm_ThroughAPole_HasTimeBelowOneAndTheEntryNormal()
        {
            FloorWorld world = World();
            ushort timber = world.MaterialId("timber");
            int pole = world.AddObject("pole", new[] { Shape(ShapeKind.Capsule, new Double3(0, 2, 0), timber, 0.1, 1.5) });
            // A 0.01 m arm along X swept 0.5 m along +Z, right through the pole: it touches when its core is 0.11 from the axis.
            var before = new Capsule(new Double3(-0.2, 2, -0.3), new Double3(0.2, 2, -0.3), 0.01);
            var after = new Capsule(new Double3(-0.2, 2, 0.2), new Double3(0.2, 2, 0.2), 0.01);
            var contacts = new StaticContact[4];
            Assert.That(world.StaticContacts(before, after, 0.02, contacts), Is.EqualTo(1));
            StaticContact c = contacts[0];
            Assert.That(c.Object, Is.EqualTo(pole));
            Assert.That(world.ObjectName(c.Object), Is.EqualTo("pole"));
            Assert.That(world.MaterialName(c.Material), Is.EqualTo("timber"));
            Assert.That(c.Time, Is.GreaterThan(0).And.LessThan(1));
            Assert.That(c.Time, Is.EqualTo(0.38).Within(Advance));
            AssertNear(c.Normal, new Double3(0, 0, -1), Advance);
            AssertNear(c.Point, new Double3(0, 2, -0.1), Advance);
            Assert.That(c.Distance, Is.EqualTo(0).Within(1e-6));
        }

        [Test]
        public void SweptSphere_ThroughAPlate_IsCaughtAndResetRemovesIt()
        {
            FloorWorld world = World();
            ShapeGeometry[] stand = { Shape(ShapeKind.Box, new Double3(0, 0.5, 0), world.MaterialId("steel"), halfExtents: new Double3(0.1, 0.01, 0.1)) };
            Assert.That(world.AddObject("stand", stand), Is.EqualTo(0));
            // A 15 mm sphere moved 0.2 m in one step through a 20 mm plate.
            var before = new Capsule(new Double3(0, 0.6, 0), new Double3(0, 0.6, 0), 0.015);
            var after = new Capsule(new Double3(0, 0.4, 0), new Double3(0, 0.4, 0), 0.015);
            var contacts = new StaticContact[4];
            Assert.That(world.StaticContacts(before, after, 0.02, contacts), Is.EqualTo(1));
            Assert.That(contacts[0].Time, Is.EqualTo(0.375).Within(Exact));
            AssertNear(contacts[0].Normal, Up, Exact);
            Assert.That(world.MaterialName(contacts[0].Material), Is.EqualTo("steel"));
            Assert.That(contacts[0].Surface, Is.EqualTo(WorldIds.NoSurface));

            world.ResetRuntimeObjects();
            Assert.That(world.StaticContacts(before, after, 0.02, contacts), Is.EqualTo(0));
            Assert.That(world.ObjectName(0), Is.Null);
            Assert.That(world.AddObject("stand", stand), Is.EqualTo(0), "after a reset the ids start again");
            Assert.That(world.StaticContacts(before, after, 0.02, contacts), Is.EqualTo(1), "one stand, one contact");
        }

        [Test]
        public void StaticContacts_AreInCanonicalOrderAndReportOverflow()
        {
            FloorWorld world = World();
            ushort steel = world.MaterialId("steel");
            world.AddObject("pair", new[]
            {
                Shape(ShapeKind.Sphere, new Double3(-0.3, 0.05, 0), steel, 0.05),
                Shape(ShapeKind.Sphere, new Double3(0.3, 0.05, 0), steel, 0.05),
            });
            var bar = new Capsule(new Double3(-0.3, 0.12, 0), new Double3(0.3, 0.12, 0), 0.03);
            var contacts = new StaticContact[4];
            Assert.That(world.StaticContacts(bar, bar, 0.1, contacts), Is.EqualTo(3));
            Assert.That(contacts[0].Object, Is.EqualTo(WorldIds.Ground));
            Assert.That(contacts[0].Distance, Is.EqualTo(0.09).Within(Exact));
            Assert.That((contacts[1].Object, contacts[1].Shape), Is.EqualTo((0, 0)));
            Assert.That((contacts[2].Object, contacts[2].Shape), Is.EqualTo((0, 1)));
            Assert.That(contacts[2].Distance, Is.EqualTo(-0.01).Within(Exact));

            var one = new StaticContact[1];
            Assert.That(world.StaticContacts(bar, bar, 0.1, one), Is.EqualTo(3), "the true count");
            Assert.That(one[0].Object, Is.EqualTo(WorldIds.Ground), "the buffer keeps the first in canonical order");
        }

        [Test]
        public void Raycast_HitsTheFloorFromAboveAndTheNearestObject()
        {
            FloorWorld world = World(0.25);
            int box = world.AddObject("box", new[]
            {
                Shape(ShapeKind.Box, new Double3(0, 1, 0), world.MaterialId("steel"), halfExtents: new Double3(0.5, 0.5, 0.5)),
            });
            var rays = new[]
            {
                new Ray(new Double3(1, 1.25, 2), new Double3(0, -2, 0)), // down onto the floor; Direction need not be unit
                new Ray(new Double3(1, 1.25, 2), new Double3(0, 1, 0)),  // up: a miss
                new Ray(new Double3(0, 3, 0), new Double3(0, -1, 0)),    // onto the box's top
                new Ray(new Double3(0, 1, 0), new Double3(0, -1, 0)),    // from inside the box: on to the floor
                new Ray(new Double3(1, 0, 2), new Double3(0, 1, 0)),     // from under the floor: a miss
            };
            var hits = new RayHit[rays.Length];
            world.Raycast(rays, 10, hits);

            Assert.That(hits[0].Distance, Is.EqualTo(1.0).Within(Exact));
            AssertNear(hits[0].Point, new Double3(1, 0.25, 2), Exact);
            AssertNear(hits[0].Normal, Up, 0);
            Assert.That(hits[0].Object, Is.EqualTo(WorldIds.Ground));
            Assert.That(world.SurfaceName(hits[0].Surface), Is.EqualTo("meadow_sod"));
            Assert.That(hits[0].Material, Is.EqualTo(WorldIds.NoMaterial));

            Assert.That(hits[1].Distance, Is.EqualTo(double.PositiveInfinity));
            Assert.That(hits[1].Surface, Is.EqualTo(WorldIds.NoSurface));

            Assert.That(hits[2].Distance, Is.EqualTo(1.5).Within(Exact));
            Assert.That(hits[2].Object, Is.EqualTo(box));
            Assert.That(world.MaterialName(hits[2].Material), Is.EqualTo("steel"));
            AssertNear(hits[2].Normal, Up, Exact);

            Assert.That(hits[3].Distance, Is.EqualTo(0.75).Within(Exact));
            Assert.That(hits[3].Object, Is.EqualTo(WorldIds.Ground));

            Assert.That(hits[4].Distance, Is.EqualTo(double.PositiveInfinity));

            var near = new RayHit[1];
            world.Raycast(new[] { rays[0] }, 0.5, near);
            Assert.That(near[0].Distance, Is.EqualTo(double.PositiveInfinity), "beyond maxDistance");
        }

        [Test]
        public void Geometry_GivesTheFloorAndEachRuntimeShape()
        {
            FloorWorld world = World(0.25);
            ShapeGeometry post = Shape(ShapeKind.Cylinder, new Double3(1, 0.375, 1), world.MaterialId("steel"), 0.0125, 0.125);
            int obj = world.AddObject("post", new[] { post });

            Assert.That(world.Geometry(WorldIds.Ground, 0, out ShapeGeometry floor), Is.True);
            Assert.That(floor.Kind, Is.EqualTo(ShapeKind.Plane));
            Assert.That(floor.Center.Y, Is.EqualTo(0.25));
            AssertNear(floor.AxisY, Up, 0);
            Assert.That(world.SurfaceName(floor.Surface), Is.EqualTo("meadow_sod"));

            Assert.That(world.Geometry(obj, 0, out ShapeGeometry g), Is.True);
            Assert.That(g.Kind, Is.EqualTo(ShapeKind.Cylinder));
            AssertNear(g.Center, post.Center, 0);
            Assert.That((g.Radius, g.HalfLength), Is.EqualTo((0.0125, 0.125)));

            Assert.That(world.Geometry(obj, 1, out _), Is.False);
            Assert.That(world.Geometry(obj + 1, 0, out _), Is.False);
            Assert.That(world.Geometry(WorldIds.Ground, 1, out _), Is.False);
        }

        [Test]
        public void AddObject_RefusesWhatARuntimeObjectCannotBe()
        {
            FloorWorld world = World();
            ushort steel = world.MaterialId("steel");
            ShapeGeometry wire = Shape(ShapeKind.Wire, default, steel, 0.005);
            ShapeGeometry plane = Shape(ShapeKind.Plane, default, steel);
            ShapeGeometry bare = Shape(ShapeKind.Sphere, default, WorldIds.NoMaterial, 0.1);
            ShapeGeometry both = Shape(ShapeKind.Sphere, default, steel, 0.1);
            both.Surface = 1;
            ShapeGeometry unknown = Shape(ShapeKind.Sphere, default, 9, 0.1);
            ShapeGeometry skewed = Shape(ShapeKind.Box, default, steel, halfExtents: new Double3(1, 1, 1));
            skewed.AxisX = new Double3(1, 0.1, 0);
            ShapeGeometry mirrored = Shape(ShapeKind.Box, default, steel, halfExtents: new Double3(1, 1, 1));
            mirrored.AxisZ = new Double3(0, 0, -1);
            foreach (ShapeGeometry s in new[] { wire, plane, bare, both, unknown, skewed, mirrored })
                Assert.Throws<ArgumentException>(() => world.AddObject("bad", new[] { s }), s.Kind.ToString());
            Assert.That(world.ObjectName(0), Is.Null, "nothing was added");
        }

        [Test]
        public void Identity_HashesTheConstructionAndNamesTheIds()
        {
            Assert.That(World(0.25).QueryVersion, Is.EqualTo(WorldHash.QueryVersion));
            Assert.That(World(0.25).ContentHash, Is.EqualTo(World(0.25).ContentHash));
            Assert.That(World(0.25).ContentHash, Is.Not.EqualTo(World(0.251).ContentHash), "a 1 mm floor move");
            Assert.That(new FloorWorld(0, "belt_bare").ContentHash, Is.Not.EqualTo(new FloorWorld(0, "meadow_sod").ContentHash));

            FloorWorld world = World();
            Assert.That(world.MaterialId("timber"), Is.EqualTo(2));
            Assert.That(world.MaterialName(2), Is.EqualTo("timber"));
            Assert.That(world.MaterialId("glass"), Is.EqualTo(WorldIds.NoMaterial));
            Assert.That(world.MaterialName(WorldIds.NoMaterial), Is.Null);
            Assert.That(world.MaterialName(3), Is.Null);
            Assert.That(world.SurfaceName(WorldIds.NoSurface), Is.Null);
        }

        [Test]
        public void BatchedEqualsOneByOne()
        {
            FloorWorld world = World(0.25);
            world.AddObject("box", new[]
            {
                Shape(ShapeKind.Box, new Double3(0, 1, 0), world.MaterialId("steel"), halfExtents: new Double3(0.5, 0.5, 0.5)),
            });
            var points = new XZ[32];
            var rays = new Ray[32];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new XZ(0.1 * i - 1.6, 0.05 * i);
                rays[i] = new Ray(new Double3(0.07 * i - 1.1, 3, 0.03 * i - 0.5), new Double3(0.01 * i - 0.16, -1, 0.02));
            }
            var batch = new GroundSample[points.Length];
            var hits = new RayHit[rays.Length];
            world.SampleGround(points, batch);
            world.Raycast(rays, 10, hits);
            var single = new GroundSample[1];
            var hit = new RayHit[1];
            for (int i = 0; i < points.Length; i++)
            {
                world.SampleGround(new[] { points[i] }, single);
                Assert.That(single[0], Is.EqualTo(batch[i]));
                world.Raycast(new[] { rays[i] }, 10, hit);
                Assert.That(hit[0], Is.EqualTo(hits[i]));
            }
        }
    }
}
