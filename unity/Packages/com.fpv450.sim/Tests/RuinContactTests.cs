using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // w4b-farmland-ruins' scenarios: the farmland village's ruin N01/Ruin_RoofCollapsed (the friend's Country House
    // FREE ruin, its physics read from the vendor mesh by tools/blender/vendor_build.py) alone on a flat map, at the
    // origin, yaw 0. The repository's drone on its legs, seeded as every contact scenario is (ContactScenarios.Setup's
    // seed), flown by SimPilot in still air through ContactScenarios.Into.
    // - Into its wall: 8 m/s north at 1.5 m into the back wall's outer face (z -4.55, the siding) at x -3.0, between
    //   back windows 2 and 3 and away from the collapsed corner: the crash names the ruin, the part (its collision
    //   shape's "part") and the material, and the contact holds the drone at the wall's surface.
    // - Through its door: from the back-left room (Room09) north at 3 m/s through partition_door1 (x -6.09..-4.79,
    //   floor to 2.79 m, 1.30 m wide: wider than the drone's 0.574 m span) along its centre line at 1.6 m: nothing
    //   touches the doorway or anything before it.
    // Each checks its premise with the map's own raycasts, so a regenerated ruin fails there, not downstream.
    public sealed class RuinContactTests
    {
        const string Variant = "Ruin_RoofCollapsed";
        const string MetaPath = "unity/Assets/_Project/Art/Models/ruins/N01/" + Variant + ".assetmeta.json";

        static MapWorld _world;
        static string[] _parts;

        static MapWorld World => _world ?? (_world = new MapWorld(Placements,
            model => System.IO.File.ReadAllText(ContactScenarios.RepositoryFile(MapWorld.ModelsRoot + "/" + model)),
            MapWorld.MaterialNames(System.IO.File.ReadAllText(ContactScenarios.RepositoryFile(MapWorld.MaterialsFile)))));

        const string Placements = "{\"format\": \"fpv450-placements\", \"format_version\": 1, \"map\": \"ruin-test\", "
            + "\"frame\": \"unity-lh-yup\", \"extent_m\": [-64.0, -64.0, 64.0, 64.0], "
            + "\"ground\": {\"plane\": {\"y_m\": 0.0, \"surface\": \"meadow_sod\"}, \"patches\": [], \"holes\": []}, "
            + "\"instances\": [{\"id\": 0, \"group\": \"village\", \"asset\": \"N01\", \"variant\": \"" + Variant + "\", "
            + "\"class\": \"ruins\", \"position_m\": [0.0, 0.0, 0.0], \"yaw_deg\": 0.0}], \"start_points\": []}";

        /// <summary>Each collision shape's "part", in the metadata's order (the shape index a contact names). The writer
        /// (lib.meta.dumps) puts one "part" in every shape and none before "gaps", so a scan of that span reads them.</summary>
        static string[] Parts
        {
            get
            {
                if (_parts != null) return _parts;
                string text = System.IO.File.ReadAllText(ContactScenarios.RepositoryFile(MetaPath));
                int from = text.IndexOf("\"collision\": [", System.StringComparison.Ordinal);
                int to = text.IndexOf("\"gaps\": [", from, System.StringComparison.Ordinal);
                var parts = new List<string>();
                foreach (Match m in Regex.Matches(text.Substring(from, to - from), "\"part\": \"([a-z_]+)\""))
                    parts.Add(m.Groups[1].Value);
                return _parts = parts.ToArray();
            }
        }

        static RayHit Cast(Double3 origin, Double3 direction)
        {
            var hits = new RayHit[1];
            World.Raycast(new[] { new Ray(origin, direction) }, 100, hits);
            return hits[0];
        }

        static ContactScenarios.PoleStrike Fly(Double3 foot, double height, double speed, List<FlightEvent> events)
        {
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong hash);
            var site = new ContactScenarios.Site(new FlightWorld(World), foot, 0);
            ContactScenarios.PoleStrike r = ContactScenarios.Into(RepositoryDrone.Spec(), m, hash, site, height, speed, _ => 0, null, 3000, 0, events);
            TestContext.Out.WriteLine($"first {r.FirstPart} on {r.FirstObject} ({r.FirstMaterial}) at {r.FirstFlightSpeed:F2} m/s; "
                + $"crash ({r.Cause}) by {r.Part} on {r.Object} ({r.Material}) at {r.FlightSpeed:F2} m/s, {r.Height:F2} m up; "
                + $"peak {r.PeakAccel:F0} m/s², impulse {r.Impulse:F2} N·s; {r.Tunnels} tunnels, {r.Lost} lost");
            return r;
        }

        [Test]
        public void TheMetadataNamesEveryShapesPart()
        {
            string text = System.IO.File.ReadAllText(ContactScenarios.RepositoryFile(MetaPath));
            int from = text.IndexOf("\"collision\": [", System.StringComparison.Ordinal);
            int shapes = Regex.Matches(text.Substring(from, text.IndexOf("\"gaps\": [", from, System.StringComparison.Ordinal) - from), "\"shape\": ").Count;
            Assert.That(Parts.Length, Is.EqualTo(shapes), "one part per collision shape");
            Assert.That(Parts, Does.Contain("wall_back").And.Contain("roof").And.Contain("floor"));
            Assert.That(World.PlacedObjects, Is.EqualTo(1));
        }

        [Test]
        public void IntoItsWallTheCrashNamesTheRuinThePartAndTheMaterial()
        {
            // The premise: at 1.5 m, 0.45 m either side of x -3.0, a ray north meets the back wall's siding first, at
            // its outer face.
            const double x = -3.0, height = 1.5, face = -4.55;
            foreach (double dx in new[] { -0.45, 0.0, 0.45 })
                foreach (double y in new[] { 1.2, 1.5, 1.8 })
                {
                    RayHit h = Cast(new Double3(x + dx, y, -30), new Double3(0, 0, 1));
                    Assert.That(h.Object, Is.EqualTo(0), $"the ray at x {x + dx}, y {y} meets the ruin");
                    Assert.That(Parts[h.Shape], Is.EqualTo("wall_back"));
                    Assert.That(h.Point.Z, Is.EqualTo(face).Within(0.03), "at the siding's face");
                }

            var events = new List<FlightEvent>();
            ContactScenarios.PoleStrike r = Fly(new Double3(x, 0, -30), height, 8, events);
            Assert.That(r.Tunnels, Is.EqualTo(0), "no tunnel: " + string.Join(", ", r.TunnelThrough));
            Assert.That(r.Lost, Is.EqualTo(0));
            Assert.That(r.FirstFlightSpeed, Is.EqualTo(8).Within(1), "the approach at 8 m/s");
            FlightEvent crash = events.Find(e => e.Kind == EventKind.Crash);
            Assert.That(r.Crashes, Is.GreaterThan(0), "a crash");
            TestContext.Out.WriteLine($"the crash's shape {crash.Shape} ({Parts[crash.Shape]}), point {crash.Point.X:F3} {crash.Point.Y:F3} {crash.Point.Z:F4}");
            Assert.That((r.Object, Parts[crash.Shape], r.Material), Is.EqualTo((Variant, "wall_back", "timber")));
            Assert.That(crash.Point.Z, Is.EqualTo(face).Within(0.03), "the contact holds it at the wall's surface");
            foreach (FlightEvent e in events)
                if (e.Kind == EventKind.ContactMade && e.Object == 0)
                    Assert.That(e.Point.Z, Is.LessThan(face + 0.05), "nothing reaches into the wall past its face");
        }

        [Test]
        public void ThroughItsDoorNothingTouchesTheDoorway()
        {
            // The premise: partition_door1 is clear along its centre line (x -5.44) from the floor to 2.79 m, and its
            // jambs stand on either side of it; the floor in Room09 behind it.
            const double x = -5.44, z = -3.2, door = -0.94;
            RayHit floor = Cast(new Double3(x, 2.0, z), new Double3(0, -1, 0));
            Assert.That(Parts[floor.Shape], Is.EqualTo("floor"));
            foreach (double y in new[] { 0.9, 1.6, 2.3 })
            {
                RayHit through = Cast(new Double3(x, y, z), new Double3(0, 0, 1));
                bool clear = double.IsPositiveInfinity(through.Distance) || through.Point.Z > door + 1.0;
                Assert.That(clear, Is.True, $"clear through the doorway at {y} m: {Parts[through.Shape]} at z {through.Point.Z:F2}");
                foreach (double dx in new[] { -0.75, 0.75 })
                {
                    RayHit jamb = Cast(new Double3(x + dx, y, z), new Double3(0, 0, 1));
                    Assert.That(jamb.Point.Z, Is.EqualTo(door).Within(0.25), $"a jamb at x {x + dx}, {y} m");
                }
            }

            var events = new List<FlightEvent>();
            ContactScenarios.PoleStrike r = Fly(new Double3(x, floor.Point.Y, z), 1.6 - floor.Point.Y, 3, events);
            Assert.That(r.Tunnels, Is.EqualTo(0), "no tunnel: " + string.Join(", ", r.TunnelThrough));
            // Every touch of the ruin but the floor it took off from lies past the doorway: none at it, none before it
            // (a crash short of the door would touch there). Past it the flight meets the next room or leaves through
            // the front wall's broken window.
            int past = 0;
            foreach (FlightEvent e in events)
            {
                if (e.Kind != EventKind.ContactMade && e.Kind != EventKind.PropStrike || e.Object != 0 || Parts[e.Shape] == "floor") continue;
                Assert.That(e.Point.Z, Is.GreaterThan(door + 0.5), $"a touch at or before the doorway: {Parts[e.Shape]} at {e.Point.X:F2} {e.Point.Y:F2} {e.Point.Z:F2}");
                if (past++ == 0) TestContext.Out.WriteLine($"first touch past the doorway: {Parts[e.Shape]} at {e.Point.X:F2} {e.Point.Y:F2} {e.Point.Z:F2}");
            }
            TestContext.Out.WriteLine($"{past} touches of the ruin past the doorway");
        }
    }
}
