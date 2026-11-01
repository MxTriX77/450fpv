using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // ground-contact's scenarios on the test range (p6-flight-core 1.6b, D-044): RangeScenarios on MapWorld. The ground
    // there is the plane, and S04's soil and E01's fillers where SampleGround reports Hole; both must carry the drone as
    // the stub's floor does, each support counted once. Each check names the known-wrong rule it fails.
    public sealed class RangeContactTests
    {
        const double G = 9.80665;

        static MaterialRow[] Materials(out ulong hash) => ContactScenarios.RepositoryMaterials(out hash);

        static DroneSpec Spec => RepositoryDrone.Spec();

        static RangeScenarios.Resting Rest(ContactScenarios.Site site)
        {
            MaterialRow[] m = Materials(out ulong h);
            RangeScenarios.Resting r = RangeScenarios.Rest(Spec, m, h, site);
            TestContext.Out.WriteLine($"sank {r.Sink * 1e3:F3} mm, {r.LoadOverWeight:F4} of the weight on the legs, {r.Pairs.Count} pairs ({r.Plane} plane, "
                + $"{r.Soil} soil, {r.Material} material), {r.HoleSupports} supports over Hole, {r.LateEvents} events in the last second: "
                + string.Join("; ", r.Pairs));
            Assert.That(r.Tunnels + r.Crashes + r.Lost, Is.EqualTo(0));
            return r;
        }

        // ---- resting ------------------------------------------------------------------------------------------------------

        [Test]
        public void AtTheTrackStartItRestsAsOnTheStub()
        {
            RangeScenarios.Resting range = Rest(RangeScenarios.At(RangeScenarios.Start("track")));
            RangeScenarios.Resting stub = Rest(default);
            Assert.That(range.Plane, Is.EqualTo(8), "each support on the plane, once");
            Assert.That(range.Pairs.Count, Is.EqualTo(8), "no skid capsule beside them (the plane is SampleGround's)");
            Assert.That(range.Sink, Is.EqualTo(stub.Sink).Within(1e-9), "the plane is the stub's floor");
            Assert.That(range.LoadOverWeight, Is.EqualTo(1).Within(1e-3));
            Assert.That(range.LateEvents, Is.EqualTo(0));
        }

        // Without the same-face rule (a skid capsule's flat-face tie at its far core end counted as a third point) both
        // flicker: 1564 and 2768 contact events in the last second, never at rest, and 0.9964 and 0.9987 of the weight.
        [TestCase("soil")]
        [TestCase("run")]
        public void OnSoilItRestsAsOnThePlane(string where)
        {
            ContactScenarios.Site site = where == "soil" ? RangeScenarios.At(RangeScenarios.Soil, 0) : RangeScenarios.At(RangeScenarios.Run, RangeScenarios.RunHeading);
            RangeScenarios.Resting soil = Rest(site);
            RangeScenarios.Resting plane = Rest(RangeScenarios.At(RangeScenarios.Start("track")));
            Assert.That(soil.HoleSupports, Is.EqualTo(8), "SampleGround reports Hole under every support (the premise)");
            Assert.That(soil.Plane, Is.EqualTo(0), "no plane inside the patch or the band");
            Assert.That(soil.Soil, Is.EqualTo(soil.Pairs.Count), "only soil shapes carry it");
            Assert.That(soil.Soil, Is.GreaterThanOrEqualTo(8), "every support on soil");
            Assert.That(soil.LoadOverWeight, Is.EqualTo(1).Within(1e-3), "the weight counted once, however many fillers a support stands on");
            Assert.That(soil.Sink, Is.EqualTo(plane.Sink).Within(1e-6), "rigid ground at G7, as the plane");
            Assert.That(soil.LateEvents, Is.EqualTo(0), "no contact flickers at rest");
            Assert.That(soil.Speed, Is.LessThan(1e-4));
        }

        [Test]
        public void OnS04sStrawItSinksIntoTheMat()
        {
            RangeScenarios.Resting r = Rest(RangeScenarios.At(RangeScenarios.Straw, 0));
            Assert.That(r.HoleSupports, Is.EqualTo(8), "over the patch (the premise)");
            Assert.That(r.Material, Is.EqualTo(r.Pairs.Count), "on the straw shapes alone");
            Assert.That(r.Plane + r.Soil, Is.EqualTo(0), "the mat holds it off the soil 0.125 m below");
            // The mat's 490 N/m in series with a leg: 11.8 N sinks a support 25 mm on its own (physics_materials.json's
            // straw row); the leg tubes' ends, 10 mm above the skids, carry part of it once the skids are past them. Rigid
            // straw would sink 1.2 mm.
            Assert.That(r.Sink, Is.InRange(0.010, 0.026));
            Assert.That(r.LateEvents, Is.EqualTo(0), "no contact flickers at rest");
            Assert.That(r.Speed, Is.LessThan(1e-3));
        }

        // ---- the landing --------------------------------------------------------------------------------------------------

        // ground-contact's "Hold the sticks and it leaves the ground", on the test range: on the plane, on S04's soil and in
        // E01's run, each as on the stub.
        [TestCase("track")]
        [TestCase("soil")]
        [TestCase("run")]
        public void HoldTheSticksAndItLeavesTheGround(string where)
        {
            MaterialRow[] m = Materials(out ulong h);
            ContactScenarios.Site site = where == "track" ? RangeScenarios.At(RangeScenarios.Start("track"))
                : where == "soil" ? RangeScenarios.At(RangeScenarios.Soil, 0) : RangeScenarios.At(RangeScenarios.Run, RangeScenarios.RunHeading);
            ContactScenarios.Landing held = ContactScenarios.Land(Spec, m, h, false, site: site);
            ContactScenarios.Landing cut = ContactScenarios.Land(Spec, m, h, true, site: site);
            ContactScenarios.Landing stub = ContactScenarios.Land(Spec, m, h, false);
            TestContext.Out.WriteLine($"held: touch {held.TouchSpeed:F3} m/s, rose {held.SeparationHeight * 1e3:F1} mm (stub {stub.SeparationHeight * 1e3:F1}); "
                + $"cut: rose {cut.SeparationHeight * 1e3:F1} mm, at rest {cut.Settled:F3} s after the touch");
            Assert.That(held.TouchSpeed, Is.EqualTo(0.5).Within(0.05));
            Assert.That(held.First.Surface, Is.Not.EqualTo(WorldIds.NoSurface), "the first touch is the ground's");
            Assert.That(held.Separated && held.SeparationHeight > 0.05, Is.True, "held: the legs throw it back into the air");
            Assert.That(held.SeparationHeight, Is.EqualTo(stub.SeparationHeight).Within(1e-3), "the ground's law is the stub floor's");
            Assert.That(cut.Settled, Is.LessThanOrEqualTo(2), "cut: it stays down within 2 s");
            Assert.That(cut.SeparationHeight, Is.LessThan(held.SeparationHeight / 10), "cut: at most a hop");
            foreach (ContactScenarios.Landing r in new[] { held, cut })
                Assert.That(r.Tunnels + r.Crashes + r.Lost, Is.EqualTo(0));
        }

        [Test]
        public void OnStrawTheMatSoaksUpTheBounce()
        {
            MaterialRow[] m = Materials(out ulong h);
            ContactScenarios.Site site = RangeScenarios.At(RangeScenarios.Straw, 0);
            ContactScenarios.Landing held = ContactScenarios.Land(Spec, m, h, false, site: site);
            ContactScenarios.Landing cut = ContactScenarios.Land(Spec, m, h, true, site: site);
            ContactScenarios.Landing plane = ContactScenarios.Land(Spec, m, h, false);
            TestContext.Out.WriteLine($"held: rose {held.SeparationHeight * 1e3:F1} mm (the plane {plane.SeparationHeight * 1e3:F1}); cut: at rest {cut.Settled:F3} s");
            Assert.That(RangeScenarios.World.MaterialName(held.First.Material), Is.EqualTo("straw"), "it lands on the mat");
            Assert.That(held.SeparationHeight, Is.LessThan(plane.SeparationHeight), "the mat's damping takes part of the legs' stored energy");
            Assert.That(cut.Settled, Is.LessThanOrEqualTo(2));
            foreach (ContactScenarios.Landing r in new[] { held, cut })
                Assert.That(r.Tunnels + r.Crashes + r.Lost, Is.EqualTo(0), "sinking through a compliant mat is no tunnel");
        }

        // ---- the field start: the stand, and a belly landing legs off --------------------------------------------------------

        [Test]
        public void LiftoffFromTheStandAtTheFieldStart()
        {
            MaterialRow[] m = Materials(out ulong h);
            StartPoint field = RangeScenarios.Start("field");
            Assert.That(field.Stand && !field.LegsOn, Is.True, "the premise: legs off, the stand");
            ContactScenarios.Liftoff r = ContactScenarios.FromStand(Spec, m, h, site: RangeScenarios.At(field));
            TestContext.Out.WriteLine($"{r.Made} made, {r.Broken} broken, off at throttle {r.Throttle:F3} (duty {r.Duty:F3}) {r.Time:F3} s into the ramp");
            Assert.That(r.MadeSteel, Is.EqualTo(8), "each arm's two edges on its post, in steel");
            Assert.That(r.RestDepth, Is.EqualTo(4.716 * G / (8 * 18000)).Within(2e-6), "resting m·g/(8·k_up) deep");
            Assert.That(r.BrokenSteel, Is.EqualTo(r.Broken));
            Assert.That(r.Broken, Is.GreaterThanOrEqualTo(8), "it leaves all four posts");
            Assert.That(r.Duty, Is.InRange(0.45, 0.65), "near hover's duty");
            Assert.That(r.ClimbAfter, Is.GreaterThan(0.05));
            Assert.That(r.Slide, Is.LessThan(0.005));
            Assert.That(r.Tunnels + r.Crashes + r.Lost, Is.EqualTo(0));
        }

        [Test]
        public void LegsOffItLandsOnItsSpool()
        {
            MaterialRow[] m = Materials(out ulong h);
            RangeScenarios.Belly r = RangeScenarios.BellyLanding(Spec, m, h, RangeScenarios.At(RangeScenarios.Start("field")));
            TestContext.Out.WriteLine($"first {r.FirstPart} at {r.TouchSpeed:F3} m/s, peak {r.PeakAccel / G:F1} g, at rest {r.Settled:F3} s on {string.Join(", ", r.Resting)}");
            Assert.That(r.FirstPart, Does.StartWith("coil"), "D-043: legs off, the spool meets the floor first");
            Assert.That(r.PeakAccel, Is.LessThan(490.3), "a 0.5 m/s belly landing is no crash");
            Assert.That(r.Settled, Is.LessThanOrEqualTo(2));
            Assert.That(r.Tunnels + r.Crashes + r.Lost, Is.EqualTo(0));
        }

        // ---- flights into the range's objects ---------------------------------------------------------------------------------

        static ContactScenarios.PoleStrike Into(string name)
        {
            MaterialRow[] m = Materials(out ulong h);
            ContactScenarios.PoleStrike r = RangeScenarios.Into(Spec, m, h, RangeScenarios.Find(name));
            TestContext.Out.WriteLine($"first {r.FirstPart} on {r.FirstObject} ({r.FirstMaterial}) by {r.First.Kind} at {r.First.A:F2} m/s flying {r.FirstFlightSpeed:F2}; "
                + $"crash ({r.Cause}) by {r.Part} on {r.Object} ({r.Material}) at {r.Speed:F2} m/s; peak {r.PeakAccel:F0} m/s², impulse {r.Impulse:F2} N·s; "
                + $"{r.Tunnels} tunnels, {r.Touched.Count} touches of the target");
            Assert.That(r.Tunnels, Is.EqualTo(0), "no tunnel: " + string.Join(", ", r.TunnelThrough));
            Assert.That(r.Lost, Is.EqualTo(0));
            return r;
        }

        // ground-contact's "Into a pole", on the test range's L01_straight.
        [Test]
        public void IntoThePole()
        {
            ContactScenarios.PoleStrike r = Into("pole");
            Assert.That(r.FlightSpeed, Is.EqualTo(10).Within(1));
            Assert.That(r.Object, Is.EqualTo("L01_straight"));
            Assert.That(r.Material, Is.EqualTo("timber"));
            Assert.That(r.Part, Is.Not.Null.And.Not.Empty);
            Assert.That(r.PeakAccel, Is.GreaterThan(490.3), "past the 50 g threshold");
            Assert.That(r.Impulse, Is.GreaterThan(4.8 * 5), "most of its momentum");
        }

        [Test]
        public void IntoHousesWall()
        {
            ContactScenarios.PoleStrike r = Into("wall");
            Assert.That(r.FirstObject, Is.EqualTo("H01_D"));
            Assert.That((r.Crashes > 0, r.Object, r.Material), Is.EqualTo((true, "H01_D", "masonry")));
        }

        [Test]
        public void IntoTheCar()
        {
            ContactScenarios.PoleStrike r = Into("car");
            Assert.That(r.FirstObject, Is.EqualTo("R01_A"));
            Assert.That(r.Crashes > 0 && r.Object == "R01_A", Is.True, "a crash on the car");
        }

        // The crown's twig shell (30 N/m) is meant to be pushed through to its bark limbs (physics_materials.json): leaving
        // a twig shape is no tunnel. With every latched pass a tunnel, this flight logged 30.
        [Test]
        public void IntoACrown()
        {
            ContactScenarios.PoleStrike r = Into("crown");
            Assert.That(r.FirstObject, Is.EqualTo("V01_L"), "the posadka's crown first");
            Assert.That(r.Touched.Count, Is.GreaterThan(0));
        }

        [Test]
        public void IntoAConductor()
        {
            ContactScenarios.PoleStrike r = Into("wire");
            Assert.That(r.FirstObject, Is.EqualTo("L03_medium"));
            Assert.That(r.FirstMaterial, Is.EqualTo("cable"));
            Assert.That(r.First.Kind, Is.EqualTo(EventKind.PropStrike), "edge-on, a prop's rim meets it first");
            Assert.That((r.Crashes > 0, r.Cause, r.Object), Is.EqualTo((true, CrashCause.PropStrike, "L03_medium")), "a rigid conductor at 10 m/s");
        }

        // review-physics §6: climbing into a conductor that crosses the front discs, clear of every body shape above them.
        // The ring catches it on the rim; the bounding sphere alone would strike 0.11 m above the disc (P4).
        [Test]
        public void ClimbingIntoAConductor()
        {
            ContactScenarios.PoleStrike r = Into("climb");
            double wire = RangeScenarios.MidSpan(RangeScenarios.Placed("L03_medium", 1), 3).Y;
            Assert.That(r.FirstObject, Is.EqualTo("L03_medium"));
            Assert.That(r.FirstMaterial, Is.EqualTo("cable"));
            Assert.That(r.First.Kind, Is.EqualTo(EventKind.PropStrike), "a prop ring meets it first");
            Assert.That(r.FirstPart, Is.EqualTo("prop_fr").Or.EqualTo("prop_fl"));
            Assert.That(r.First.Point.Y, Is.EqualTo(wire).Within(0.01), "on the lowest conductor, at the rim's height");
            Assert.That(r.First.A, Is.GreaterThan(1), "closing at the climb's speed");
        }

        // review-physics §6 and P3: a conductor across the skids between their spheres is caught by the skid capsules.
        [Test]
        public void ASkidAcrossAConductor()
        {
            MaterialRow[] m = Materials(out ulong h);
            RangeScenarios.OnWire r = RangeScenarios.OntoWire(Spec, m, h);
            TestContext.Out.WriteLine($"first {r.FirstShape} at {r.TouchSpeed:F3} m/s; {string.Join(", ", r.Shapes)}; peak leg {r.PeakLegLoad:F1} N; "
                + $"{r.After:F3} m over it 1.5 s later; {r.Strikes} strikes");
            Assert.That(r.FirstShape, Does.StartWith("skid_"), "the skid capsule, not a support sphere: it crosses between them");
            Assert.That(r.TouchSpeed, Is.EqualTo(2).Within(0.4));
            Assert.That(r.PeakLegLoad, Is.GreaterThan(0), "the leg carries it");
            Assert.That(r.Tunnels + r.Lost, Is.EqualTo(0));
        }

        // review-physics §6 and W6: a rotor's down ray meets a conductor, and cast again from inside it goes on to the ground.
        [Test]
        public void ARotorRayPastAConductor()
        {
            MapWorld w = RangeScenarios.World;
            int line = RangeScenarios.Placed("L03_medium", 1);
            Double3 wire = RangeScenarios.MidSpan(line, 3);
            Assert.That(w.Geometry(line, 3, out ShapeGeometry g), Is.True);
            var hub = new Double3(wire.X, wire.Y + 0.1, wire.Z);
            var hits = new RayHit[2];
            w.Raycast(new[] { new Ray(hub, new Double3(0, -1, 0)) }, 10, new System.Span<RayHit>(hits, 0, 1));
            w.Raycast(new[] { new Ray(new Double3(hub.X, wire.Y, hub.Z), new Double3(0, -1, 0)) }, 10, new System.Span<RayHit>(hits, 1, 1));
            Assert.That(hits[0].Object, Is.EqualTo(line));
            Assert.That(hits[0].Distance, Is.EqualTo(0.1 - g.Radius).Within(1e-6), "the conductor's top");
            Assert.That(hits[1].Object, Is.EqualTo(WorldIds.Ground), "from its axis, past it to the ground");
            Assert.That(hits[1].Distance, Is.EqualTo(wire.Y).Within(1e-9));
        }
    }
}
