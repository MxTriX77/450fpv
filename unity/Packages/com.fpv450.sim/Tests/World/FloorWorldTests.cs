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

        // ---- No false first touch (QA's review B3, QueryVersion 3) -----------------------------------------------------

        [Test]
        public void SkidSlidingJustAboveTheFloor_IsNeverAFirstTouch_AtAnySpeed()
        {
            // A level skid (r 5 mm) 0.1 mm over the floor, sliding 10, 20 and 30 mm in a step (10–30 m/s at 1 kHz): it never
            // touches, so the floor is the current contact (Time 1). The distance / speed advancement alone crept 0.1 mm at a
            // time and gave up as a first touch at Time 0.64, 0.32 and 0.213.
            FloorWorld world = World();
            const double r = 0.005, y = r + 0.0001;
            var contacts = new StaticContact[4];
            foreach (double move in new[] { 0.010, 0.020, 0.030 })
            {
                var before = new Capsule(new Double3(0, y, 0), new Double3(0.12, y, 0), r);
                var after = new Capsule(new Double3(move, y, 0), new Double3(0.12 + move, y, 0), r);
                Assert.That(world.StaticContacts(before, after, 0.02, contacts), Is.EqualTo(1), $"{move} m");
                Assert.That(contacts[0].Object, Is.EqualTo(WorldIds.Ground));
                Assert.That(contacts[0].Time, Is.EqualTo(1), $"{move} m: never touched");
                Assert.That(contacts[0].Distance, Is.EqualTo(0.0001).Within(Exact), $"{move} m");
            }
        }

        [Test]
        public void GrazingDescentOntoTheFloor_IsItsExactFirstTouch()
        {
            // A 5 mm sphere 40 µm over the floor, moving 20 mm across it while sinking 50 µm: it touches at t = 0.8 exactly.
            // The distance / speed steps would take 0.2 % of the step at a time; the plane's bound is its distance, so its
            // step lands on the touch, with Distance 0 to rounding.
            FloorWorld world = World();
            const double r = 0.005;
            var before = new Capsule(new Double3(0, r + 0.00004, 0), new Double3(0, r + 0.00004, 0), r);
            var after = new Capsule(new Double3(0.02, r - 0.00001, 0), new Double3(0.02, r - 0.00001, 0), r);
            var contacts = new StaticContact[4];
            Assert.That(world.StaticContacts(before, after, 0.02, contacts), Is.EqualTo(1));
            Assert.That(contacts[0].Time, Is.EqualTo(0.8).Within(Exact));
            Assert.That(contacts[0].Distance, Is.EqualTo(0).Within(Exact));
            AssertNear(contacts[0].Point, new Double3(0.016, 0, 0), Exact);
        }

        [Test]
        public void SphereSlidingPastATrunk_IsNeverAFirstTouch()
        {
            // A 5 mm sphere sliding 20 mm in a step along a tangent of a 0.15 m trunk cylinder, 0.2 mm off it at the
            // closest (half way): it never touches, so the trunk is the current contact. The distance / speed advancement
            // alone gave up as a first touch (QA's Z07 case: Time 0.836, Distance 0.34 mm).
            FloorWorld world = World();
            int trunk = world.AddObject("trunk", new[] { Shape(ShapeKind.Cylinder, new Double3(0, 2, 0), world.MaterialId("timber"), 0.15, 2) });
            const double r = 0.005, off = 0.15 + r + 0.0002;
            var before = new Capsule(new Double3(-0.01, 1, off), new Double3(-0.01, 1, off), r);
            var after = new Capsule(new Double3(0.01, 1, off), new Double3(0.01, 1, off), r);
            var contacts = new StaticContact[4];
            Assert.That(world.StaticContacts(before, after, 0.02, contacts), Is.EqualTo(1));
            Assert.That(contacts[0].Object, Is.EqualTo(trunk));
            Assert.That(contacts[0].Time, Is.EqualTo(1), "never touched");
            Assert.That(contacts[0].Distance, Is.EqualTo(Math.Sqrt(off * off + 0.0001) - 0.15 - r).Within(1e-9));
        }

        // ---- The bound is safe on every primitive (QA's re-check S3) ----------------------------------------------------

        private static Double3 Unit(Double3 v) => v * (1 / Math.Sqrt(Double3.Dot(v, v)));

        /// <summary>
        /// A 7.5 mm sphere moved 60 mm in one step: either past a curved feature (a sphere's centre, a capsule's or a
        /// cylinder's axis, a rim, a box edge or corner: `point`, reached at `reach` from it) at 0.990–0.998 of `reach`, out
        /// along `away` (in the feature's own region) and moving along `along` (square to `away`); or onto a flat face
        /// (`point` on it, outward normal `away`, moving along `along` in it) from 0.1–1 mm over it to 15–75 µm into it. Each
        /// is one contact of `obj` with Time in (0, 1), not after the analytic first touch, Distance within 1 µm of 0. The
        /// distance / speed steps creep on these grazes, so the separating plane's bound settles them: a support that
        /// leaves out part of the shape steps past the touch or calls the sweep clear.
        /// </summary>
        private static void AssertGrazeCaught(FloorWorld world, int obj, Double3 point, Double3 away, Double3 along, double reach, bool flat,
            Random rng, string what)
        {
            const double r = 0.0075, step = 0.06;
            Double3 from, to;
            double touch;
            if (flat)
            {
                double above = 0.0001 + 0.0009 * rng.NextDouble(), into = (0.002 + 0.008 * rng.NextDouble()) * r;
                from = point - along * (step / 2) + away * (r + above);
                to = point + along * (step / 2) + away * (r - into);
                touch = above / (above + into);
            }
            else
            {
                double pass = (0.990 + 0.008 * rng.NextDouble()) * reach;
                from = point + away * pass - along * (step / 2);
                to = point + away * pass + along * (step / 2);
                touch = 0.5 - Math.Sqrt(reach * reach - pass * pass) / step;
            }
            var contacts = new StaticContact[8];
            int n = world.StaticContacts(new Capsule(from, from, r), new Capsule(to, to, r), 0.05, contacts);
            Assert.That(n, Is.LessThanOrEqualTo(contacts.Length), what + ": overflow");
            StaticContact[] mine = Array.FindAll(contacts.AsSpan(0, n).ToArray(), c => c.Object == obj);
            Assert.That(mine.Length, Is.EqualTo(1), what + ": the sphere passed unseen");
            Assert.That(mine[0].Time, Is.GreaterThan(0).And.LessThan(1), what + ": Time");
            Assert.That(mine[0].Time, Is.LessThanOrEqualTo(touch + 1e-9), what + ": Time after the first touch");
            Assert.That(mine[0].Distance, Is.EqualTo(0).Within(1e-6), what + ": Distance at the first touch");
        }

        [Test]
        public void GrazingSpheres_AreCaughtNotAfterTheirFirstTouch_OnEveryPrimitive()
        {
            // Mutations that leave out a sphere's radius, a box's z extent or a cylinder's radius from the bound's support
            // passed every world test before this one; only the goldens' hashes moved.
            FloorWorld world = World();
            ushort steel = world.MaterialId("steel");
            // Exact 3-4-5 turns (yaw, then tilt about the new x), so every axis of the box and the cylinder is off the world's.
            Double3 ax = new Double3(0.8, 0, -0.6), ay = new Double3(0.36, 0.8, 0.48), az = new Double3(0.48, -0.6, 0.64);
            ShapeGeometry Turned(ShapeGeometry s)
            {
                s.AxisX = ax;
                s.AxisY = ay;
                s.AxisZ = az;
                return s;
            }
            Double3 sphereAt = new Double3(0, 2, 0), capsuleAt = new Double3(3, 2, 0), cylinderAt = new Double3(6, 2, 0), boxAt = new Double3(9, 2, 0);
            Double3 h = new Double3(0.5, 0.3, 0.4);
            int sphere = world.AddObject("sphere", new[] { Shape(ShapeKind.Sphere, sphereAt, steel, 0.1) });
            int capsule = world.AddObject("capsule", new[] { Turned(Shape(ShapeKind.Capsule, capsuleAt, steel, 0.05, 0.4)) });
            int cylinder = world.AddObject("cylinder", new[] { Turned(Shape(ShapeKind.Cylinder, cylinderAt, steel, 0.15, 0.4)) });
            int box = world.AddObject("box", new[] { Turned(Shape(ShapeKind.Box, boxAt, steel, halfExtents: h)) });
            Double3[] axes = { ax, ay, az };
            double[] half = { h.X, h.Y, h.Z };
            const double r = 0.0075;
            var rng = new Random(2611);
            Double3 Square(Double3 v) // a random unit vector square to the unit v
            {
                Double3 p = Unit(Double3.Cross(v, Math.Abs(v.Y) < 0.9 ? Up : new Double3(1, 0, 0))), q = Double3.Cross(v, p);
                double a = 2 * Math.PI * rng.NextDouble();
                return p * Math.Cos(a) + q * Math.Sin(a);
            }
            double Sign() => rng.Next(2) == 0 ? 1 : -1;

            for (int trial = 0; trial < 150; trial++)
            {
                // A sphere: past its centre, any way.
                Double3 away = Unit(new Double3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5));
                AssertGrazeCaught(world, sphere, sphereAt, away, Square(away), 0.1 + r, false, rng, $"sphere {trial}");

                // A capsule and a cylinder's side: square past their axis, inside the length.
                away = Square(ay);
                AssertGrazeCaught(world, capsule, capsuleAt + ay * (0.6 * rng.NextDouble() - 0.3), away, Double3.Cross(ay, away) * Sign(),
                    0.05 + r, false, rng, $"capsule {trial}");
                away = Square(ay);
                AssertGrazeCaught(world, cylinder, cylinderAt + ay * (0.5 * rng.NextDouble() - 0.25), away, Double3.Cross(ay, away) * Sign(),
                    0.15 + r, false, rng, $"cylinder side {trial}");

                // A cylinder's cap: onto it, inside its rim. Its rim: past a rim point, out between the side's and the cap's normals.
                double end = Sign();
                Double3 radial = Square(ay);
                Double3 onCap = cylinderAt + ay * (0.4 * end) + radial * (0.1 * rng.NextDouble());
                AssertGrazeCaught(world, cylinder, onCap, ay * end, Square(ay), 0, true, rng, $"cylinder cap {trial}");
                double tilt = (0.25 + 0.5 * rng.NextDouble()) * Math.PI / 2;
                away = radial * Math.Cos(tilt) + ay * (end * Math.Sin(tilt));
                Double3 across = (radial * -Math.Sin(tilt) + ay * (end * Math.Cos(tilt))) * Sign();
                AssertGrazeCaught(world, cylinder, cylinderAt + ay * (0.4 * end) + radial * 0.15, away, across, r, false, rng, $"cylinder rim {trial}");

                // A box: onto each face, past each edge and each corner.
                int i = rng.Next(3), j = (i + 1) % 3, k = (i + 2) % 3;
                double si = Sign(), sj = Sign(), sk = Sign();
                Double3 onFace = boxAt + axes[i] * (si * half[i]) + axes[j] * ((2 * rng.NextDouble() - 1) * (half[j] - 0.05))
                    + axes[k] * ((2 * rng.NextDouble() - 1) * (half[k] - 0.05));
                AssertGrazeCaught(world, box, onFace, axes[i] * si, Square(axes[i]), 0, true, rng, $"box face {i} {trial}");
                Double3 onEdge = boxAt + axes[i] * (si * half[i]) + axes[j] * (sj * half[j]) + axes[k] * ((1.6 * rng.NextDouble() - 0.8) * half[k]);
                tilt = (0.25 + 0.5 * rng.NextDouble()) * Math.PI / 2;
                away = axes[i] * (si * Math.Cos(tilt)) + axes[j] * (sj * Math.Sin(tilt));
                AssertGrazeCaught(world, box, onEdge, away, Double3.Cross(axes[k], away) * Sign(), r, false, rng, $"box edge {k} {trial}");
                Double3 corner = boxAt + axes[0] * (si * half[0]) + axes[1] * (sj * half[1]) + axes[2] * (sk * half[2]);
                away = Unit(axes[0] * (si * (0.5 + 0.5 * rng.NextDouble())) + axes[1] * (sj * (0.5 + 0.5 * rng.NextDouble()))
                    + axes[2] * (sk * (0.5 + 0.5 * rng.NextDouble())));
                AssertGrazeCaught(world, box, corner, away, Square(away), r, false, rng, $"box corner {trial}");
            }
        }

        // ---- The line contact on a box face (review-physics W5) ---------------------------------------------------------

        /// <summary>The contact of a level 5 mm skid 1 mm over the 1 m box's top face (y 1), at rest, from a to b at the face's height.</summary>
        private static StaticContact OnTheBox(FloorWorld world, Double3 a, Double3 b)
        {
            const double r = 0.005, y = 1 + r + 0.001;
            var skid = new Capsule(new Double3(a.X, y, a.Z), new Double3(b.X, y, b.Z), r);
            var contacts = new StaticContact[4];
            Assert.That(world.StaticContacts(skid, skid, 0.02, contacts), Is.EqualTo(1), "the box alone, the floor beyond the margin");
            Assert.That(contacts[0].Time, Is.EqualTo(1));
            Assert.That(contacts[0].Distance, Is.EqualTo(0.001).Within(Exact));
            AssertNear(contacts[0].Normal, Up, Exact);
            return contacts[0];
        }

        [Test]
        public void FlatSkidOnABoxFace_TouchesAtTheStartOfItsFlatStretch()
        {
            // IWorldQuery's line contact: a skid lying along a face is equally near all along its flat stretch, and the
            // contact is that stretch's start, A itself when A lies over the face (physics' skid rule relies on it).
            FloorWorld world = World();
            world.AddObject("block", new[] { Shape(ShapeKind.Box, new Double3(0, 0.5, 0), world.MaterialId("steel"), halfExtents: new Double3(0.5, 0.5, 0.5)) });

            // Both ends over the face: A's foot, in either order.
            AssertNear(OnTheBox(world, new Double3(-0.2, 0, 0.1), new Double3(0.3, 0, 0.1)).Point, new Double3(-0.2, 1, 0.1), Exact);
            AssertNear(OnTheBox(world, new Double3(0.3, 0, 0.1), new Double3(-0.2, 0, 0.1)).Point, new Double3(0.3, 1, 0.1), Exact);

            // A over the face, B 0.25 m past its edge (x 0.5): the flat stretch [A, edge] and the edge tie exactly at the
            // edge's u = 0.5, and the stretch's start, A, is the contact. From B's side the stretch starts at the edge.
            AssertNear(OnTheBox(world, new Double3(0.25, 0, 0), new Double3(0.75, 0, 0)).Point, new Double3(0.25, 1, 0), Exact);
            AssertNear(OnTheBox(world, new Double3(0.75, 0, 0), new Double3(0.25, 0, 0)).Point, new Double3(0.5, 1, 0), Exact);
        }

        [Test]
        public void FlatSkidOnAYawedBoxFace_TouchesAtAsFoot()
        {
            // The same on a box yawed by an exact 3-4-5 rotation: its x axis (0.8, 0, −0.6), its z axis (0.6, 0, 0.8).
            FloorWorld world = World();
            Double3 ax = new Double3(0.8, 0, -0.6), az = new Double3(0.6, 0, 0.8);
            ShapeGeometry block = Shape(ShapeKind.Box, new Double3(0, 0.5, 0), world.MaterialId("steel"), halfExtents: new Double3(0.5, 0.5, 0.5));
            block.AxisX = ax;
            block.AxisZ = az;
            world.AddObject("block", new[] { block });
            Double3 a = ax * -0.2 + az * 0.1, b = ax * 0.3 + az * 0.1;
            AssertNear(OnTheBox(world, a, b).Point, new Double3(a.X, 1, a.Z), Exact);
            AssertNear(OnTheBox(world, b, a).Point, new Double3(b.X, 1, b.Z), Exact);
            Double3 over = ax * 0.25, past = ax * 0.75;
            AssertNear(OnTheBox(world, over, past).Point, new Double3(over.X, 1, over.Z), Exact);
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
