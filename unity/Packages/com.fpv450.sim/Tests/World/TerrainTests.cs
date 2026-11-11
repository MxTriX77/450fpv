using System;
using System.IO;
using System.Security.Cryptography;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // A map package's terrain (w3-farmland-ground 1.1) on a package written here, where every answer is analytic: two flat
    // tiles side by side, "high" on [0, 4] × [0, 4] at 5 m and "low" on [4, 8] × [0, 4] at 2 m, so a 3 m step at x = 4
    // whose face looks east; and a hole in the high tile's south-west cell. Heights are samples × 1 m (size_m y 32766).
    public sealed class TerrainTests
    {
        private const double Exact = 1e-9; // closed forms over coordinates of a few metres: only rounding
        private const double Advance = 1e-5; // conservative advancement stops within 1 µm of the surface
        private static readonly Double3 East = new Double3(1, 0, 0);
        private static string _dir;
        private static MapWorld _world;

        private static MapWorld World => _world ?? (_world = Mini());

        [OneTimeTearDown]
        public void Remove()
        {
            if (_dir != null && Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }

        /// <summary>Writes the package (and the material table it loads with) under a fresh folder and loads it.</summary>
        private static MapWorld Mini()
        {
            _dir = Path.Combine(Path.GetTempPath(), "fpv450-terrain-" + Guid.NewGuid().ToString("N"));
            string maps = Path.Combine(_dir, "maps", "mini");
            Directory.CreateDirectory(maps);
            Directory.CreateDirectory(Path.Combine(_dir, "assets-src"));
            File.Copy(Path.Combine(MapWorldTests.Root, MapWorld.MaterialsFile), Path.Combine(_dir, MapWorld.MaterialsFile));
            byte[] Heights(int sample)
            {
                var b = new byte[2 * 9];
                for (int i = 0; i < 9; i++)
                {
                    b[2 * i] = (byte)sample;
                    b[2 * i + 1] = (byte)(sample >> 8);
                }
                return b;
            }
            var files = new (string Name, byte[] Data)[]
            {
                ("heights/high.r16", Heights(5)), ("heights/low.r16", Heights(2)),
                ("classes/high.raw", new byte[] { 1, 1, 1, 1 }), ("classes/low.raw", new byte[] { 13, 13, 13, 13 }),
                ("holes/high.raw", new byte[] { 0, 0, 255, 0 }), // row 0 north: the south-west cell is the hole
            };
            string listed = "";
            foreach (var f in files)
            {
                string path = Path.Combine(maps, f.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, f.Data);
                using (SHA256 sha = SHA256.Create())
                    listed += (listed.Length > 0 ? "," : "") + "\"" + f.Name + "\":\"" + BitConverter.ToString(sha.ComputeHash(f.Data)).Replace("-", "").ToLowerInvariant() + "\"";
            }
            File.WriteAllText(Path.Combine(maps, "map.json"), "{\"format\":\"fpv450-map-package\",\"format_version\":1,\"map\":\"mini\",\"frame\":\"unity-lh-yup\","
                + "\"map_rect_m\":[0,0,4,4],\"tiles\":["
                + "{\"id\":\"high\",\"ring\":\"map\",\"position_m\":[0,0,0],\"size_m\":[4,32766,4],\"heights\":{\"file\":\"heights/high.r16\",\"resolution\":3},"
                + "\"classes\":{\"file\":\"classes/high.raw\",\"resolution\":2},\"holes\":{\"file\":\"holes/high.raw\",\"resolution\":2}},"
                + "{\"id\":\"low\",\"ring\":\"surround\",\"position_m\":[4,0,0],\"size_m\":[4,32766,4],\"heights\":{\"file\":\"heights/low.r16\",\"resolution\":3},"
                + "\"classes\":{\"file\":\"classes/low.raw\",\"resolution\":2}}],"
                + "\"files\":{" + listed + "}}");
            return MapWorld.Load(_dir, "mini");
        }

        private static GroundSample Sample(double x, double z)
        {
            var g = new GroundSample[1];
            World.SampleGround(new[] { new XZ(x, z) }, g);
            return g[0];
        }

        private static RayHit Cast(Double3 o, Double3 d, double reach = 10)
        {
            var h = new RayHit[1];
            World.Raycast(new[] { new Ray(o, d) }, reach, h);
            return h[0];
        }

        private static int Contacts(Double3 from, Double3 to, double r, double margin, out StaticContact c)
        {
            var buffer = new StaticContact[4];
            int n = World.StaticContacts(new Capsule(from, from, r), new Capsule(to, to, r), margin, buffer);
            c = buffer[0];
            return n;
        }

        [Test]
        public void SampleGround_IsTheHighestTileWithItsClassAndAHoleWhereTheMaskSaysSo()
        {
            GroundSample high = Sample(3, 3), low = Sample(6, 1), step = Sample(4, 3), hole = Sample(1, 1), off = Sample(-0.01, 2);
            Assert.That(high.Height, Is.EqualTo(5).Within(Exact));
            Assert.That((high.Surface, high.Flags), Is.EqualTo((MapWorld.SurfaceId("meadow_sod"), GroundFlags.None)));
            Assert.That(high.Normal.Y, Is.EqualTo(1).Within(Exact));
            Assert.That(low.Height, Is.EqualTo(2).Within(Exact));
            Assert.That(low.Surface, Is.EqualTo(MapWorld.SurfaceId("dirt_track")));
            Assert.That(step.Height, Is.EqualTo(5).Within(Exact), "on the step line the top is the higher side's");
            foreach (GroundSample g in new[] { hole, off })
            {
                Assert.That(g.Flags, Is.EqualTo(GroundFlags.Hole));
                Assert.That(g.Height, Is.EqualTo(double.NegativeInfinity));
                Assert.That((g.Normal.X, g.Normal.Y, g.Normal.Z, g.Surface), Is.EqualTo((0.0, 1.0, 0.0, WorldIds.NoSurface)));
            }
            Assert.That(World.Geometry(WorldIds.Ground, 0, out _), Is.False, "a terrain has no one ground geometry");
        }

        [Test]
        public void Load_RefusesAFileThatIsNotTheManifests()
        {
            Assert.That(World, Is.Not.Null); // writes the package
            string from = Path.Combine(_dir, "maps", "mini"), to = Path.Combine(_dir, "maps", "bad");
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string copy = Path.Combine(to, file.Substring(from.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.Copy(file, copy);
            }
            byte[] low = File.ReadAllBytes(Path.Combine(to, "heights", "low.r16"));
            low[4] ^= 1;
            File.WriteAllBytes(Path.Combine(to, "heights", "low.r16"), low);
            var e = Assert.Throws<FormatException>(() => MapWorld.Load(_dir, "bad"));
            Assert.That(e.Message, Does.Contain("heights/low.r16"));
        }

        [Test]
        public void Load_DerivesTheStepFromTheTiles()
        {
            TerrainStep[] steps = World.TerrainSteps;
            Assert.That(steps.Length, Is.EqualTo(1));
            Assert.That((steps[0].AlongZ, steps[0].C, steps[0].From, steps[0].To, steps[0].Rings), Is.EqualTo((true, 4.0, 0.0, 4.0, "map|surround")));
            Assert.That(steps[0].Least, Is.EqualTo(3).Within(Exact));
            Assert.That(steps[0].Most, Is.EqualTo(3).Within(Exact));
        }

        [Test]
        public void Raycast_EntersTheFaceFromTheLowerSideAndTheTopFromAbove()
        {
            RayHit face = Cast(new Double3(6, 3.5, 3), new Double3(-1, 0, 0));
            Assert.That((face.Object, face.Surface), Is.EqualTo((WorldIds.Ground, MapWorld.SurfaceId("meadow_sod"))));
            Assert.That(face.Distance, Is.EqualTo(2).Within(Exact));
            Assert.That((face.Normal.X, face.Normal.Y, face.Normal.Z), Is.EqualTo((1.0, 0.0, 0.0)), "the face looks out of the higher side");
            RayHit over = Cast(new Double3(6, 5.5, 3), new Double3(-1, 0, 0), 3);
            Assert.That(over.Distance, Is.EqualTo(double.PositiveInfinity), "above the step it passes over");
            RayHit off = Cast(new Double3(3, 3.5, 3), East);
            Assert.That(off.Distance, Is.EqualTo(double.PositiveInfinity), "from inside the high side's column: no hit");
            RayHit down = Cast(new Double3(3, 7, 3), new Double3(0, -1, 0));
            Assert.That(down.Distance, Is.EqualTo(2).Within(Exact));
            Assert.That(down.Normal.Y, Is.EqualTo(1));
            RayHit slant = Cast(new Double3(7, 4, 3), new Double3(-0.6, -0.8, 0));
            Assert.That(slant.Distance, Is.EqualTo(2.5).Within(Exact), "down onto the low side at x = 5.5");
            Assert.That(slant.Point.X, Is.EqualTo(5.5).Within(Exact));
            RayHit hole = Cast(new Double3(1, 7, 1), new Double3(0, -1, 0));
            Assert.That(hole.Distance, Is.EqualTo(double.PositiveInfinity), "through the hole cell: nothing below");
        }

        [Test]
        public void StaticContacts_ASweepAcrossTheStepTouchesTheFaceNeverTheTop()
        {
            const double r = 0.015;
            int n = Contacts(new Double3(4.1, 3.5, 3), new Double3(3.9, 3.5, 3), r, 0.05, out StaticContact c);
            Assert.That(n, Is.EqualTo(1));
            Assert.That((c.Object, c.Shape, c.Surface), Is.EqualTo((WorldIds.Ground, 0, MapWorld.SurfaceId("meadow_sod"))));
            Assert.That(c.Time, Is.EqualTo((0.1 - r) / 0.2).Within(Advance), "the first touch, latched");
            Assert.That(c.Point.X, Is.EqualTo(4).Within(Exact));
            Assert.That(c.Normal.X, Is.EqualTo(1).Within(Exact), "horizontal, out of the higher side");
            Assert.That(c.Distance, Is.EqualTo(0).Within(Advance));
        }

        [Test]
        public void StaticContacts_InsideBehindTheFaceIsAsDeepAsTheFaceNotTheTop()
        {
            const double r = 0.015;
            Double3 x = new Double3(3.98, 3.5, 3); // 2 cm behind the face, 1.5 m under the top
            Assert.That(Contacts(x, x, r, 0.05, out StaticContact c), Is.EqualTo(1));
            Assert.That(c.Distance, Is.EqualTo(-0.02 - r).Within(Exact), "never a 1.5 m jump to the top");
            Assert.That((c.Normal.X, c.Normal.Y, c.Normal.Z), Is.EqualTo((1.0, 0.0, 0.0)));
            Assert.That(c.Point.X, Is.EqualTo(4).Within(Exact));
            Assert.That(c.Time, Is.EqualTo(1));
            // Straddling the face with the low side's facets in reach and the high side's top 3 m above it, far out of reach:
            // the end 5 cm behind the face is 5 cm deep (a regression: the top's depth was once taken from facets in reach only).
            var buffer = new StaticContact[4];
            var straddle = new Capsule(new Double3(4.05, 2.03, 3), new Double3(3.95, 2.05, 3), 0.01);
            Assert.That(World.StaticContacts(straddle, straddle, 0.05, buffer), Is.EqualTo(1));
            Assert.That(buffer[0].Distance, Is.EqualTo(-0.05 - 0.01).Within(Exact));
            Assert.That((buffer[0].Normal.X, buffer[0].Normal.Y, buffer[0].Normal.Z), Is.EqualTo((1.0, 0.0, 0.0)));
            Double3 deep = new Double3(2.5, 4.99, 3); // under the top, 1.5 m from the face
            Assert.That(Contacts(deep, deep, r, 0.05, out c), Is.EqualTo(1));
            Assert.That(c.Distance, Is.EqualTo(-0.01 - r).Within(Exact));
            Assert.That(c.Normal.Y, Is.EqualTo(1).Within(Exact));
        }

        [Test]
        public void StaticContacts_OnTheTopWithinTheMarginAndNoneOverTheHole()
        {
            const double r = 0.015;
            Double3 rest = new Double3(3, 5 + r - 0.002, 3);
            Assert.That(Contacts(rest, rest, r, 0.05, out StaticContact c), Is.EqualTo(1));
            Assert.That(c.Distance, Is.EqualTo(-0.002).Within(Exact));
            Assert.That(c.Point.X, Is.EqualTo(3).Within(Exact));
            Assert.That(c.Point.Y, Is.EqualTo(5).Within(Exact));
            Assert.That(c.Point.Z, Is.EqualTo(3).Within(Exact));
            Double3 above = new Double3(3, 5 + r + 0.03, 3);
            Assert.That(Contacts(above, above, r, 0.05, out c), Is.EqualTo(1));
            Assert.That(c.Distance, Is.EqualTo(0.03).Within(Exact));
            Assert.That(Contacts(above, above, r, 0.02, out _), Is.EqualTo(0), "beyond the margin");
            Double3 hole = new Double3(1, 5 + r - 0.002, 1);
            Assert.That(Contacts(hole, hole, r, 0.05, out _), Is.EqualTo(0), "over the hole cell the asset's shapes are the ground");
            Double3 off = new Double3(9, 1, 2);
            Assert.That(Contacts(off, off, r, 0.05, out _), Is.EqualTo(0), "off the world");
        }

        [Test]
        public void StaticContacts_AFallOntoTheTopLatchesItsFirstTouch()
        {
            const double r = 0.02;
            int n = Contacts(new Double3(3, 5 + r + 0.04, 3), new Double3(3, 5 + r - 0.02, 3), r, 0.05, out StaticContact c);
            Assert.That(n, Is.EqualTo(1));
            Assert.That(c.Time, Is.EqualTo(0.04 / 0.06).Within(Advance));
            Assert.That(c.Distance, Is.EqualTo(0).Within(Advance));
            Assert.That(c.Normal.Y, Is.EqualTo(1).Within(Exact));
        }
    }
}
