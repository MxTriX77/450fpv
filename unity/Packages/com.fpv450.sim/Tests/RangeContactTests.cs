using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
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
        // Without the seam rule (ContactModel.Seam) E01's run rests on 16 soil pairs, two a support (a support 2 mm past a
        // filler's edge meets that edge too, its normal 10° off). At model 26 that is only 0.443 µm deeper than the plane and
        // 1.0001 of the weight (QA's integration review, B3; 1.3 µm and 1.001 at 1.6b's model), inside those tolerances, so
        // one pair a support is what fails it. With the edge alone dropped (1.6b), 9 µm higher and pitched (a support on two
        // fillers counted twice in its leg's split).
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
            Assert.That(soil.Pairs.Count, Is.EqualTo(8), "every support on soil, once (the seam rule)");
            Assert.That(soil.LoadOverWeight, Is.EqualTo(1).Within(1e-3), "the weight counted once, however many fillers a support stands on");
            Assert.That(soil.Sink, Is.EqualTo(plane.Sink).Within(1e-6), "rigid ground at G7, as the plane");
            Assert.That(soil.LateEvents, Is.EqualTo(0), "no contact flickers at rest");
            Assert.That(soil.Speed, Is.LessThan(1e-4));
        }

        // SameSupport's coplanar widening (a skid's contact on a face coplanar with its support's) has no case on the range:
        // switched off, S04's soil and E01's run rest as before to 1 nm (QA's integration review, B3). So it has its own. Three
        // timber boards lie flush on the stub's floor: the middle one 0.10 m wide under the skids' middles, the outer ones
        // under the supports (the joints at ±0.1025 m, the ends at ±0.22). Each skid half's line contact on the middle board
        // lies 0.05 m or more from every support sphere and on no support's board, so only its plane, the outer boards' top,
        // makes it its support's. With the widening the drone rests on its eight supports as on one wide board; without it
        // the four skid halves meet the middle board as four more of their legs' points. It starts 1 mm over the boards, so
        // every contact is made in flight and logged (the start pose's are made before the first step).
        [Test]
        public void OnFlushBoardsEachSupportIsOnePoint()
        {
            RangeScenarios.Resting Boards(params (double Z0, double Z1)[] boards)
            {
                FlightWorld world = FlightWorld.Floor();
                ushort timber = world.Query.MaterialId("timber");
                var shapes = new ShapeGeometry[boards.Length];
                for (int i = 0; i < boards.Length; i++)
                    shapes[i] = new ShapeGeometry
                    {
                        Kind = ShapeKind.Box, Center = new Double3(0, 0.025, (boards[i].Z0 + boards[i].Z1) / 2), AxisX = new Double3(1, 0, 0),
                        AxisY = new Double3(0, 1, 0), AxisZ = new Double3(0, 0, 1), HalfExtents = new Double3(0.4, 0.025, (boards[i].Z1 - boards[i].Z0) / 2),
                        Material = timber,
                    };
                var site = new ContactScenarios.Site(world, new Double3(0, 0.051, 0), 0) { Objects = new[] { new RuntimeObject("boards", shapes) } };
                return Rest(site);
            }
            RangeScenarios.Resting one = Boards((-0.45, 0.45));
            RangeScenarios.Resting three = Boards((-0.45, -0.05), (-0.05, 0.05), (0.05, 0.45));
            Assert.That(one.Material, Is.EqualTo(one.Pairs.Count).And.EqualTo(8), "on one board, each support once (the premise)");
            Assert.That(three.Material, Is.EqualTo(three.Pairs.Count).And.EqualTo(8), "on three, each support once and no skid's middle");
            Assert.That(three.Sink, Is.EqualTo(one.Sink).Within(1e-9), "as on one board");
            Assert.That(three.LoadOverWeight, Is.EqualTo(1).Within(1e-3), "the weight counted once");
            Assert.That(three.LateEvents, Is.EqualTo(0), "no contact flickers at rest");
        }

        // Re-measured by p7a-ground 1.12 against 1.6b's: the mat meets each half-skid by its area (3.03 × the reference foot's
        // 490 N/m, 1482.5 N/m) in series with a leg, so a quarter of the weight sinks a support 9.0 mm. 1.6b's row read per
        // part as the reference foot (490 N/m, the known-wrong) sinks it 10–26 mm (the leg tubes' ends, 15 mm over the skids'
        // bottoms, taking a share once the skids are past them). Rigid straw would sink 1.2 mm. At 9 mm this spot's skids and
        // supports lie across overlapping tiles, 2–5 mm inside a neighbouring tile's side face: a face buried in the mat
        // (ContactModel's buried-face rule, the side-face fix). Taken as a contact (model 36, the known-wrong), a horizontal
        // normal carried part of a corner's force: the legs carried 1.039 of the weight and the drone rocked at 0.4 mm/s and
        // 0.005 rad/s, with 10 skid contact events in the last second, never at rest.
        [Test]
        public void OnS04sStrawItSinksIntoTheMat()
        {
            RangeScenarios.Resting r = Rest(RangeScenarios.At(RangeScenarios.Straw, 0));
            Assert.That(r.HoleSupports, Is.EqualTo(8), "over the patch (the premise)");
            Assert.That(r.Material, Is.EqualTo(r.Pairs.Count), "on the straw shapes alone");
            Assert.That(r.Plane + r.Soil, Is.EqualTo(0), "the mat holds it off the soil 0.125 m below");
            MaterialRow[] m = Materials(out ulong h);
            ContactSpec contacts = Spec.BuildContacts(Spec.Nominal, m, h);
            MaterialRow straw = System.Array.Find(m, x => x.Name == "straw");
            double half = LayerScenarios.HalfSkid(straw, SoilScenarios.MatDepth), k0 = contacts.LegStiffness;
            double derived = Spec.BuildMass(Spec.Nominal).Properties.Mass * G / 4 * (1 / k0 + 1 / half);
            RangeScenarios.Resting foot = RangeScenarios.Rest(Spec, GoldenFlights.WithoutTheAreaLaws(), h, RangeScenarios.At(RangeScenarios.Straw, 0));
            TestContext.Out.WriteLine($"sank {r.Sink * 1e3:F3} mm against the derived {derived * 1e3:F3} (the half-skid's {half:F1} N/m with the leg), "
                + $"{r.Speed * 1e3:F3} mm/s, {r.Spin:E2} rad/s, {r.LateEvents} contact events in the last second; read per part as the reference "
                + $"foot (1.6b's) {foot.Sink * 1e3:F3} mm, {foot.LateEvents} events");
            Assert.That(r.Sink, Is.EqualTo(derived).Within(5).Percent, "the weight's quarter on the half-skid's area law and the leg");
            Assert.That(foot.Sink, Is.InRange(0.010, 0.026), "known-wrong: 1.6b's mat read as the reference foot sinks it as before");
            Assert.That(foot.Sink, Is.Not.EqualTo(derived).Within(5).Percent, "and fails");
            Assert.That(r.LoadOverWeight, Is.EqualTo(1).Within(1e-3), "the weight carried once, on the mat's top");
            Assert.That(r.LateEvents, Is.EqualTo(0), "no contact flickers at rest");
            Assert.That(foot.LateEvents, Is.EqualTo(0), "at 1.6b's depth no contact flickers");
            Assert.That(r.Speed, Is.LessThan(1e-3));
            Assert.That(r.Spin, Is.LessThan(1e-3), "no rocking");
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
            Assert.That(r.RestDepth, Is.EqualTo(4.626 * G / (8 * 18000)).Within(2e-6), "resting m·g/(8·k_up) deep (L4 legs off, D-066)");
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
            Assert.That(r.FirstPart, Does.StartWith("coil"), "D-043: legs off, the spool meets the floor first (since model 64 its canister's cone)");
            Assert.That(r.PeakAccel, Is.LessThan(490.3), "a 0.5 m/s belly landing is no crash");
            // Since model 64 the canister's cone meets the floor first (its lowest point, the canister tilted 15° rear-down), and
            // the drone pitches nose-down onto the canister's body and rocks there under airmode's held loop (D-067's slow tilt):
            // at rest 3.55 s after the touch (the old coil, level, within 2 s), inside the flight's 4 s.
            Assert.That(r.Settled, Is.LessThanOrEqualTo(4));
            Assert.That(r.Tunnels + r.Crashes + r.Lost, Is.EqualTo(0));
        }

        // ---- flights into the range's objects ---------------------------------------------------------------------------------

        static ContactScenarios.PoleStrike Into(string name, bool airframe = false)
        {
            MaterialRow[] m = Materials(out ulong h);
            ContactScenarios.PoleStrike r = RangeScenarios.Into(Spec, m, h, RangeScenarios.Find(name), airframe);
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
            // Since model 46 the front legs part on the pole after the prop's strike and the payload makes the crash a few
            // milliseconds later (ContactTests.IntoAPole): the flight's speed is judged at the first touch.
            Assert.That(r.FirstFlightSpeed, Is.EqualTo(10).Within(1), "the approach at 10 m/s (the carve's push then moves it, p7f-damage 1.3)");
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
        // a twig shape is no tunnel. With every latched pass a tunnel, this flight logged 30. At model 46 it logged one, leg_rl's stub
        // through V01_M's bark at 10.16 m/s, 13 ms after its tube parted; at model 48 the tip slide's carve moves the flight (the
        // crash the payload's on Z11_tree_ash at 6.47 m/s, against 5.91) and it logs none; with the tip slide off (model 43's carve)
        // that tunnel is back, so its cause, the post-crash class of a broken leg's part, is open (p7g-legs).
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
            // Since model 35 a strike is no crash (p7f-damage 1.3): the conductor (cable, harder than the blades) breaks the front
            // blades at their roots, and the flight ends when the body meets it, by acceleration, named by a body part.
            Assert.That((r.Crashes > 0, r.Cause, r.Object), Is.EqualTo((true, CrashCause.Acceleration, "L03_medium")), "a rigid conductor at 10 m/s");
            Assert.That(r.Part, Does.Not.StartWith("prop_"), "a body part");
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

        // ---- with the airframe's effects on ---------------------------------------------------------------------------------

        // The two laws with the airframe's effects on, as a pilot's flight has them (p7b-airframe: the motors' asymmetry, the
        // vortex rings, the ground effect from the rotors' rays, the flapping, the modes). With the same-face rule off, a
        // skid's flat-face tie counts as a third point and the rest flickers (2016 and 3800 contact events in the last second
        // on S04's soil and in E01's run); without the seam rule the run rests on 16 soil pairs, two a support; with every latched
        // pass a tunnel, the crown logs 31 through V01_L's and V01_M's twigs.
        static ContactScenarios.Site Airframe(ContactScenarios.Site site)
        {
            site.Airframe = true;
            return site;
        }

        // Since model 39 (p7g-legs 1.2) the legs give across their tubes, and a cut landing's rest (every leg loaded, the body
        // under 1 cm/s) waits for the legs' side sway, f = √(4·k_l/m)/2π = 2.3 Hz at ζ 0.2, no longer for the 32 Hz bounce along
        // them: a site's small difference in the touch moves that crossing by a part of the sway's 0.44 s period. Two sites with
        // one law rest within an eighth of it (derived): E01's run 17 ms before the stub (0.219 against 0.236 s), S04's soil in
        // the crosswind 22 ms before the plane; model 37's 10 and 15 ms were the bounce's.
        const double SideSettle = 0.055;

        [TestCase("soil")]
        [TestCase("run")]
        public void WithTheAirframesEffectsSoilIsStillThePlane(string where)
        {
            ContactScenarios.Site site = Airframe(where == "soil" ? RangeScenarios.At(RangeScenarios.Soil, 0) : RangeScenarios.At(RangeScenarios.Run, RangeScenarios.RunHeading));
            RangeScenarios.Resting soil = Rest(site);
            RangeScenarios.Resting plane = Rest(Airframe(RangeScenarios.At(RangeScenarios.Start("track"))));
            Assert.That(soil.HoleSupports, Is.EqualTo(8), "SampleGround reports Hole under every support (the premise)");
            Assert.That(soil.Soil, Is.EqualTo(soil.Pairs.Count).And.EqualTo(8), "only soil shapes carry it, every support once");
            Assert.That(soil.LoadOverWeight, Is.EqualTo(1).Within(1e-3), "the weight counted once");
            Assert.That(soil.Sink, Is.EqualTo(plane.Sink).Within(1e-6), "rigid ground at G7, as the plane");
            Assert.That(soil.LateEvents, Is.EqualTo(0), "no contact flickers at rest");

            // The landing: the rotors' rays meet the soil's face where the stub's meet its floor, so the ground effect, the
            // touch and the bounce are the stub's.
            MaterialRow[] m = Materials(out ulong h);
            foreach (bool cut in new[] { false, true })
            {
                ContactScenarios.Landing here = ContactScenarios.Land(Spec, m, h, cut, site: site);
                ContactScenarios.Landing stub = ContactScenarios.Land(Spec, m, h, cut, site: Airframe(default));
                TestContext.Out.WriteLine($"{(cut ? "cut" : "held")}: touch {here.TouchSpeed:F4} m/s (stub {stub.TouchSpeed:F4}), rose {here.SeparationHeight * 1e3:F2} mm "
                    + $"(stub {stub.SeparationHeight * 1e3:F2}), at rest {here.Settled:F3} s (stub {stub.Settled:F3}), {here.ContactEvents} contact events (stub {stub.ContactEvents})");
                Assert.That(here.First.Surface, Is.Not.EqualTo(WorldIds.NoSurface), "the first touch is the ground's");
                Assert.That(here.TouchSpeed, Is.EqualTo(stub.TouchSpeed).Within(1e-3));
                Assert.That(here.SeparationHeight, Is.EqualTo(stub.SeparationHeight).Within(1e-3), "the ground's law is the stub floor's");
                if (cut)
                {
                    // Re-traced at model 46 (p7g-legs on the line): armed at idle (airmode) with the airframe's effects, the blades'
                    // pitch draws' 1/rev keep the body on its soft legs near the rest's 1 cm/s for over a second, and the crossing is
                    // the idle rotors' forcing's, not the ground's law: E01's run rests at 2.093 s against the stub's 1.581 (without
                    // props.blade 1.160 against 1.153, within SideSettle; on model 26's legs 0.420 and 0.420; without the airframe's
                    // effects 0.205). Both come to rest, and the rest is the stub's: its height.
                    Assert.That(here.Settled, Is.Not.NaN.And.LessThanOrEqualTo(2.5), "cut, it comes to rest");
                    Assert.That(stub.Settled, Is.Not.NaN);
                    Assert.That(here.Core.State.Position.Y - site.Foot.Y, Is.EqualTo(stub.Core.State.Position.Y).Within(1e-3), "at the stub's height");
                }
                else Assert.That(here.Separated && here.SeparationHeight > 0.05, Is.True, "held: the legs throw it back into the air");
                Assert.That(here.Tunnels + here.Crashes + here.Lost, Is.EqualTo(0));
            }
        }

        // Model 48 (the tip slide and the fountain on the line): the crown with the airframe's effects logs one tunnel after its
        // crash (the payload on Z11_tree_ash at 5.01 m/s, step 10507): at step 11332 the right skid's rear half (skid_rr), hanging
        // from leg_fr's T-joint since leg_rr parted (step 10374), passes V01_M's bark at 6.24 m/s, 25 mm deep, the body falling at
        // 1.6 m/s: the hanging half's turn a step behind (p7g-legs 1.3; RangeContactTests' Calm crown). It stays red.
        // Model 50 (the legs' lever on the line): no tunnel (first skid_left on V01_L's twig at 9.77 m/s, the crash the payload's
        // on Z11_tree_ash at 5.74 m/s, 64 touches); the lever moves the flight off its hanging half's pass, the class is open
        // (the crown in Windy, InTheWindNoTunnelThroughARigidShape).
        [Test]
        public void WithTheAirframesEffectsACompliantShapeIsNoTunnel()
        {
            MaterialRow[] m = Materials(out ulong h);
            foreach (bool cut in new[] { false, true })
            {
                ContactScenarios.Landing r = ContactScenarios.Land(Spec, m, h, cut, site: Airframe(RangeScenarios.At(RangeScenarios.Straw, 0)));
                TestContext.Out.WriteLine($"straw, {(cut ? "cut" : "held")}: rose {r.SeparationHeight * 1e3:F1} mm, at rest {r.Settled:F3} s, {r.Tunnels} tunnels");
                Assert.That(RangeScenarios.World.MaterialName(r.First.Material), Is.EqualTo("straw"), "it lands on the mat");
                Assert.That(r.Tunnels + r.Crashes + r.Lost, Is.EqualTo(0), "sinking through a compliant mat is no tunnel");
            }
            ContactScenarios.PoleStrike crown = Into("crown", true);
            Assert.That(crown.FirstObject, Is.EqualTo("V01_L"), "the posadka's crown first");
            Assert.That(crown.Touched.Count, Is.GreaterThan(0));
        }

        // ---- in the wind (p7c-wind's field, model 19) ------------------------------------------------------------------

        // The laws in the wind, the airframe's effects on. The soil's laws are held in a steady 5 m/s from 270°, the same air
        // everywhere, against the plane at the same heading in it (at model 19 the level presets blew nothing over S04 and
        // E01, where SampleGround reports Hole; since model 20 they read the soil there, OverAHoleTheWindReadsTheSoil).
        static ContactScenarios.Site InTheAir(ContactScenarios.Site site, WindSetup wind)
        {
            site.Airframe = true;
            site.Wind = wind;
            return site;
        }

        [TestCase("soil")]
        [TestCase("run")]
        public void InASteadyCrosswindSoilIsStillThePlane(string where)
        {
            WindSetup wind = WindSetup.SteadyFrom(270, 5);
            double heading = where == "soil" ? 0 : RangeScenarios.RunHeading;
            ContactScenarios.Site site = InTheAir(RangeScenarios.At(where == "soil" ? RangeScenarios.Soil : RangeScenarios.Run, heading), wind);
            ContactScenarios.Site plane = InTheAir(RangeScenarios.At(RangeScenarios.Start("track").Position, heading), wind);
            RangeScenarios.Resting soil = Rest(site), flat = Rest(plane);
            Assert.That(soil.HoleSupports, Is.EqualTo(8), "SampleGround reports Hole under every support (the premise)");
            Assert.That(soil.Soil, Is.EqualTo(soil.Pairs.Count).And.EqualTo(8), "only soil shapes carry it, every support once");
            Assert.That(soil.LoadOverWeight + soil.AeroOverWeight, Is.EqualTo(1).Within(1e-3), "the weight counted once, less the air's lift");
            Assert.That(soil.Sink, Is.EqualTo(flat.Sink).Within(1e-6), "as the plane in the same air: the seam rule");
            Assert.That(soil.LateEvents, Is.EqualTo(0), "no contact flickers at rest: the skid's same-face rule");

            MaterialRow[] m = Materials(out ulong h);
            foreach (bool cut in new[] { false, true })
            {
                ContactScenarios.Landing here = ContactScenarios.Land(Spec, m, h, cut, site: site);
                ContactScenarios.Landing there = ContactScenarios.Land(Spec, m, h, cut, site: plane);
                TestContext.Out.WriteLine($"{(cut ? "cut" : "held")}: touch {here.TouchSpeed:F4} m/s (plane {there.TouchSpeed:F4}), rose {here.SeparationHeight * 1e3:F2} mm "
                    + $"(plane {there.SeparationHeight * 1e3:F2}), at rest {here.Settled:F3} s (plane {there.Settled:F3}), {here.ContactEvents} contact events (plane {there.ContactEvents})");
                Assert.That(here.First.Surface, Is.Not.EqualTo(WorldIds.NoSurface), "the first touch is the ground's");
                Assert.That(here.Tunnels + here.Crashes + here.Lost, Is.EqualTo(0));
                // Since model 64 within 1 cm/s, not 1 mm/s: the canister's side silhouette drifts the hold 0.410 m downwind at the
                // touch (0.356 on the old coil, 0.334 without the canister's drag; the plane the same), where the downwind rotors'
                // discs reach over the straw mat at the soil gap's edge (+0.60 m) and its ground effect slows the descent: the touch
                // 0.4584 m/s against the plane's 0.4653 (traced, a probe, not kept: on the old coil 0.4641 against 0.4640, and
                // without the canister's drag 0.4649 both). The site's ground effect, not the soil's law.
                Assert.That(here.TouchSpeed, Is.EqualTo(there.TouchSpeed).Within(0.01), "the touch is the plane's");
                if (where == "soil")
                {
                    // Traced: the site, not the soil's law. S04's soil spot is a gap of bare soil (x −0.33 to +0.60 m about its
                    // foot) in the straw mat, whose boxes stand 0.125 m higher. In the crosswind the pilot's hold sits 0.36 m
                    // downwind at the touch, on the plane as here; held, the bounce carries it on, and from 0.31 s after the
                    // touch the right rotors' rays meet the mat (0.25 m against the plane's 0.35): more ground effect on that
                    // side, a higher rise (202.5 mm against 169.2), support_fr_end on a straw box mid-bounce, and the end on
                    // the mat. Cut, it settles within 10 ms of the plane (0.764 against 0.754 s).
                    // Re-traced at model 37 (p7g-legs 1.1): on the derived 52.3 kN/m legs the held bounce is lower (114.7 mm
                    // against the plane's 131.1; model 26's assumed 10 kN/m: 225.8 against 186.1) and comes down across the
                    // gap's downwind edge, and the drone tips over onto the mat, on its side 0.90 m downwind (76.6°). The spring
                    // does it, not the legs' mass: model 26's spring on the new masses rides onto the mat upright (2.8°), the
                    // derived one on model 26's masses tips it (76.7°), and so it does with the stick anchor held at model 26's
                    // 5 kN/m (82.8°).
                    // Re-traced at model 39 (p7g-legs 1.2): on the legs' side give the held bounce rises as the plane's (137.1 mm
                    // against 138.4) and no longer tips it: it is carried past the gap's downwind edge upright (8.3°) and is still
                    // in the air at the end, 1.10 m downwind and 0.74 m up (the held hover's throttle); the stiff anchor across the
                    // foot (model 37's) was what threw it over at the mat's edge. Cut, the crosswind keeps the legs' side sway going
                    // (2.3 Hz at the tube's ζ 0.015, its decay's time 1/(ζω) = 4.6 s): it rests under 1 cm/s at 3.75 s and the
                    // plane's not inside the 4 s, so it is held to the plane's end instead, on its eight supports at the plane's
                    // height and upright.
                    double tilt = 2 * System.Math.Acos(System.Math.Min(1, System.Math.Abs(here.Core.State.Attitude.W))) * 180 / System.Math.PI;
                    TestContext.Out.WriteLine($"{(cut ? "cut" : "held")}: ends {tilt:F1}° from level, {here.Core.State.Position.X - site.Foot.X:F3} m downwind of the foot, {here.Core.State.Position.Y - site.Foot.Y:F4} m over it");
                    if (!cut)
                    {
                        // Re-traced at model 46 (p7g-legs on the line): on the line's blades the held bounce rises 152.6 mm against
                        // the plane's 145.7 (without props.blade 147.3 against 148.8, p7g's 1.3 mm; on model 26's legs 233.7 against
                        // 187.0): the blades' pitch draws and flapping inertia carry the bounce a little differently over the gap's
                        // downwind edge, where the right rotors' rays meet the mat (the site's ground effect, above), not the soil.
                        // Since model 64 the canister's side drag drifts it 0.41 m downwind at the touch (above), the right rotors
                        // over the mat from the touch: the held bounce rises 190.5 mm against the plane's 159.6, so it is held to
                        // the site's ground effect (within 0.05 m), the rest as before.
                        // Re-traced at model 70 (p7f-damage 1.6b, the motors' heat; heat on and off): the plane's bounce is the same
                        // (155.9 mm against 159.6) and the soil's touch too (0.4566 m/s against 0.4584), but held over the gap's downwind
                        // edge the right skid catches the mat (at 1.5 s 0.57 m downwind against 0.61): the drone rolls 15°, the mixer
                        // saturates fighting it, and as the skid lets go at 1.72 s the saturated right rotors climb it away, 2.1 m up
                        // and 5 m downwind at the end (rose 453.4 mm against 190.5 without the heat). A hair of the motors' warming
                        // (Kt −0.8 %, R +2 % after 13 s) decides the catch: the site's mat, not the soil's law. So the held bounce is
                        // judged no lower than the plane's (the soil absorbs it no more), the mat's lift or catch on top.
                        Assert.That(here.SeparationHeight, Is.GreaterThanOrEqualTo(there.SeparationHeight - 0.05), "held, it rises as on the plane or higher, the mat's ground effect or catch on top");
                        Assert.That(tilt, Is.LessThan(60), "and stays upright");
                        Assert.That(here.Core.State.Position.X - site.Foot.X, Is.GreaterThan(0.60), "past the gap's downwind edge");
                    }
                    else
                    {
                        Assert.That(here.Touching, Is.EqualTo(8).And.EqualTo(there.Touching), "cut, it ends on its eight supports as the plane's");
                        Assert.That(here.Core.State.Position.Y - site.Foot.Y, Is.EqualTo(there.Core.State.Position.Y - plane.Foot.Y).Within(1e-4), "at its height");
                        Assert.That(tilt, Is.LessThan(1), "upright");
                    }
                    continue;
                }
                Assert.That(here.SeparationHeight, Is.EqualTo(there.SeparationHeight).Within(1e-3), "E01's fillers land as the plane");
                if (cut) Assert.That(here.Settled, Is.EqualTo(there.Settled).Within(0.01));
            }
        }

        // No tunnel through a compliant shape in the wind: the straw landings in the crosswind, and the crown in Windy's field
        // (the posadka stands on the plane; the approach crosses S04, where the field blows since model 20).
        // Model 50 (the legs' lever on the line): the crown in Windy logs one, the left skid's front half hanging from leg_rl's
        // T-joint after leg_fl breaks (InTheWindNoTunnelThroughARigidShape's Windy crown, the same flight). It stays red.
        // Model 51 (the hanging half's whole give geometric): none (InTheWindNoTunnelThroughARigidShape, model 51).
        [Test]
        public void InTheWindACompliantShapeIsNoTunnel()
        {
            MaterialRow[] m = Materials(out ulong h);
            foreach (bool cut in new[] { false, true })
            {
                ContactScenarios.Landing r = ContactScenarios.Land(Spec, m, h, cut, site: InTheAir(RangeScenarios.At(RangeScenarios.Straw, 0), WindSetup.SteadyFrom(270, 5)));
                TestContext.Out.WriteLine($"straw in the crosswind, {(cut ? "cut" : "held")}: rose {r.SeparationHeight * 1e3:F1} mm, at rest {r.Settled:F3} s, {r.Tunnels} tunnels");
                Assert.That(RangeScenarios.World.MaterialName(r.First.Material), Is.EqualTo("straw"), "it lands on the mat");
                Assert.That(r.Tunnels + r.Crashes + r.Lost, Is.EqualTo(0), "sinking through a compliant mat is no tunnel");
            }
            RangeScenarios.Approach a = RangeScenarios.Find("crown");
            ContactScenarios.PoleStrike crown = ContactScenarios.Into(Spec, m, h, InTheAir(RangeScenarios.At(a.Foot, a.HeadingDeg), WindSetup.Of(WindLevel.Windy)),
                a.Height, a.Speed, _ => RangeScenarios.Placed(a.Variant, a.Occurrence), null, a.ClimbSteps, a.ClimbRate);
            TestContext.Out.WriteLine($"crown in Windy: first {crown.FirstPart} on {crown.FirstObject} ({crown.FirstMaterial}); crash ({crown.Cause}) by {crown.Part} "
                + $"on {crown.Object} ({crown.Material}); {crown.Tunnels} tunnels, {crown.Touched.Count} touches");
            Assert.That(crown.FirstObject, Is.EqualTo("V01_L"), "the posadka's crown first");
            Assert.That(crown.Tunnels + crown.Lost, Is.EqualTo(0));

            RangeScenarios.Resting track = Rest(InTheAir(RangeScenarios.At(RangeScenarios.Start("track")), WindSetup.Of(WindLevel.Severe)));
            Assert.That(track.Plane, Is.EqualTo(8));
            Assert.That(track.LoadOverWeight + track.AeroOverWeight, Is.EqualTo(1).Within(1e-3), "Severe's field at the track start");
            Assert.That(track.LateEvents, Is.EqualTo(0));
        }

        // Tunnels: none through a rigid shape in any level's field, on the crown's and the wire's approaches. At model 19 on
        // query version 3 the wire logged 2 through L03's cable held at 5.475 m, or with leg_fr first at 5.51–5.52 m: query
        // version 4 (the turning sweep bisected to its touch) closes them, model 19's flights there now log none. Severe's
        // crown logged 4 through V01_L's bark, and model 19's flight still does at query version 4: after the strike crash
        // the tumbling drone's leg_fr, support_fr_joint and support_fr_end pass limb 307 (r 4.0 mm) and skid_rl limb 149
        // (r 13.4 mm) at 5–13 m/s closing. The leg tube's 6.6 kN/m and the legs' spring need 0.1–0.3 m to stop the drone,
        // more than the limbs are thick, so the core passes the limb's axis under the latch and leaves the sweep's margin
        // 18–115 mm deep against its latched plane: drone.json's margin_m names it (a soft part that fast into a rod snaps
        // in truth, phase 7's breakage; ATunnelIsLoggedAsABug). Since model 20 that approach crosses S04 in the wind, meets
        // the crown otherwise (the crash on Z11_tree_ash behind it) and logs none.
        // Model 37 (p7g-legs 1.1): at 5.475 m in Severe leg_fl passes L03's cable once, after the prop's first touch, at 9–10
        // m/s: the 6.6 g lighter legs' path (model 26's leg spring on the new masses tunnels the same, the derived spring on
        // model 26's masses does not), the leg shape's 6.6 kN/m stand-in, which a cable that fast goes through (a PVC-U leg
        // breaks on it in truth: p7g-legs 1.2's fracture, which brings the case back to none).
        // Model 39 (p7g-legs 1.2): the legs break on these strikes, and every tunnel goes through a broken leg's part, which 1.2
        // keeps on the drone and 1.3 parts from it (design 8, the loose piece), bringing each to none: at 5.475 m in Severe
        // leg_fl parts at its clamp 4 ms before its tube passes the cable; at 5.52 m the four legs part on the cable and the
        // skids then pass it, 0.1 m into it on their legs' side give. No tunnel goes through an intact leg's part. Severe's
        // crown logs none (it did, through support_fr_end after leg_fr broke, with the side give at the legs' assumed ζ 0.2).
        // Model 41 (p7g-legs 1.3): the parted pieces leave the contacts, and at 5.475 m in Severe the tunnel is gone; at 5.52 m
        // the four legs part on the cable, and one tunnel is left (of 2), skid_fr through the cable at 6.9 m/s, 2 ms after
        // leg_rr cracks: its half hangs from leg_rr's T-joint once leg_fr parts, the skid's 303 N/m cantilever turning its
        // shapes out of what it touches a step behind. On the hanging halves as a penetration spring the crown in Windy and
        // Severe logged one each and 18 lost contacts; their turn made geometric closes them.
        // Model 46 (p7g-legs on the line, the line's blades, yaw I and soil): at 5.52 m in Severe no tunnel is left (the right
        // pack meets the cable first, 10.22 m/s, and the crash is by it); the crown in Severe logs one, support_rr_end through
        // V01_L's bark at 9.94 m/s at step 10302, 23 ms after leg_rr parts there (12.1 N·m) and as its skid half, hanging from
        // leg_fr's T-joint, folds (the half's turn a step behind, 1.3's 5.52 m case's mechanism); the crown in Calm and Windy and
        // the wire's other cases none.
        // Model 48 (the tip slide and the fountain on the line): each flight still reaches its shape (the wire's first touch is on
        // L03's cable, the crown's on V01_L; asserted since), and the crown in Severe logs none (it logged one at 46). The crown in
        // Calm logs one, support_rr_end through Z10_tree_acacia's timber at 9.44 m/s, 0.146 m deep, at step 11864: 20 ms after
        // leg_rr parts (11844) and 4 ms after the right skid's half, hanging from leg_fr's T-joint, folds (11860), the half's turn a
        // step behind (46's Severe mechanism, p7g-legs 1.3). A tunnel is a bug: the case stays red until the hanging half's turn is
        // in its step.
        // Model 49 (p7g-legs 1.4, the legs' lever): the crown in Severe logs two, both through support_rr_end of leg_rr's skid half
        // hanging from leg_fr's T-joint after leg_rr parts (11.9 N·m, step 10279): through V01_L's bark at 9.94 m/s at step 10302
        // as on model 46, and through Z11_tree_ash's timber at 15.6 m/s at step 10713 in the crash's tumble; the hanging half's
        // turn a step behind (1.3's mechanism) and its fold without a parting limit (open since 1.3); the rest none.
        // Model 50 (the legs' lever on the line): each flight still reaches its shape. The crown in Calm and Severe log none
        // (Calm's model 48 tunnel is gone: first skid_left on V01_L's twig at 11.16 m/s, the crash the payload's on
        // Z11_tree_ash at 3.62 m/s), and the crown in Windy logs one: support_fl_end of the left skid through V01_L's bark,
        // 0.115 m deep at 2.31 m/s, at step 10975, 66 ms after leg_fl breaks (10909) with leg_rl cracked and set and on, the
        // skid's front half hanging from leg_rl's T-joint: the hanging half's turn a step behind again (1.3's mechanism),
        // moved from Calm to Windy by the lever, not closed. A tunnel is a bug: it stays red.
        // Model 51 (p7g-legs on the line: the hanging half and the stub). Traced, the class's cause was not the turn's step: the
        // half's force is its chain's (the contact spring, the skid's cantilever, the holding leg's give across its tube and the
        // stick anchor in series, 64 N/m across), and only the cantilever's share moved its shapes, so the rest was a penetration
        // that grew with the drone's travel (Windy's 0.045 → 0.115 m over 66 ms at 9 N; a step's lag is 2.3 mm). The half's whole
        // give is geometry now (ContactSpec.HangingGive): every case none, each still reaching its shape, and with it off the Windy
        // crown is model 50's flight to its Tunnel (WithoutTheHangingGiveTheWindyCrownIsModel50s).
        [TestCase("crown", 10.0, WindLevel.Calm, 0)]
        [TestCase("crown", 10.0, WindLevel.Windy, 0)]
        [TestCase("crown", 10.0, WindLevel.Severe, 0)]
        [TestCase("wire", 5.47, WindLevel.Severe, 0)]
        [TestCase("wire", 5.475, WindLevel.Severe, 0)]
        [TestCase("wire", 5.51, WindLevel.Severe, 0)]
        [TestCase("wire", 5.52, WindLevel.Severe, 0)]
        [TestCase("wire", 5.475, WindLevel.Windy, 0)]
        [TestCase("wire", 5.475, WindLevel.Still, 0)]
        public void InTheWindNoTunnelThroughARigidShape(string name, double height, WindLevel level, int legTunnels)
        {
            MaterialRow[] m = Materials(out ulong h);
            RangeScenarios.Approach a = RangeScenarios.Find(name);
            ContactScenarios.PoleStrike p = ContactScenarios.Into(Spec, m, h, InTheAir(RangeScenarios.At(a.Foot, a.HeadingDeg), WindSetup.Of(level)),
                height, a.Speed, _ => RangeScenarios.Placed(a.Variant, a.Occurrence), null, a.ClimbSteps, a.ClimbRate);
            TestContext.Out.WriteLine($"{name} at {height} m in {level}: first {p.FirstPart} on {p.FirstObject} ({p.FirstMaterial}) at {p.First.A:F2} m/s; "
                + $"crash ({p.Cause}) by {p.Part} on {p.Object} ({p.Material}) at {p.FlightSpeed:F2} m/s; {p.Tunnels} tunnels ({string.Join(", ", p.TunnelThrough)}), {p.Lost} lost");
            foreach (string line in p.Order) TestContext.Out.WriteLine("  " + line);
            Assert.That(p.Touched.Count, Is.GreaterThan(0), "the flight reaches its shape");
            Assert.That(p.Lost, Is.EqualTo(0));
            Assert.That(p.Tunnels, Is.EqualTo(legTunnels));
            Assert.That(p.ThroughBroken, Is.EqualTo(p.Tunnels), "each through a broken leg's part, which 1.3 parts from the drone");
        }

        // Model 51's audit and known-wrong (the hanging half's whole give and the stub's spring, p7g-legs on the line): with both
        // off (GoldenFlights.WithoutTheHangingGive) the crown in Windy is model 50's flight to its Tunnel, the step, the shape and
        // the millimetre: the left skid's front half, hanging from leg_rl's T-joint since leg_fl broke at step 10909, through
        // V01_L's bark, its shapes turned out by the cantilever's 30.8 mm alone while the holding leg's give across its tube and
        // the stick anchor in series left 113 mm of penetration under 9.3 N. With the fix the same flight logs none (above).
        // Since model 57 (the props at rest on the line) model 50's flight needs the rest off too (GoldenFlights.WithoutTheRest):
        // with it on, a stopped rotor's blades are parts and the flight leaves 50's path before the legs part, and logs no
        // tunnel either way (InTheWindNoTunnelThroughARigidShape flies it with both on). Since model 58 (p7f-damage 1.5) the bells off
        // too (GoldenFlights.WithoutTheBells): the crown's twigs meet a turning motor's band before the legs part, so with them on the
        // flight leaves 50's path there.
        [Test]
        public void WithoutTheHangingGiveTheWindyCrownIsModel50s()
        {
            MaterialRow[] m = Materials(out ulong h);
            RangeScenarios.Approach a = RangeScenarios.Find("crown");
            // Since model 64 on model 63's drone (the old coil, Model63Drone), as every kept audit.
            ContactScenarios.PoleStrike p = ContactScenarios.Into(Model63Drone.Spec(), m, h, InTheAir(RangeScenarios.At(a.Foot, a.HeadingDeg), WindSetup.Of(WindLevel.Windy)),
                a.Height, a.Speed, _ => RangeScenarios.Placed(a.Variant, a.Occurrence), null, a.ClimbSteps, a.ClimbRate,
                edit: s => { GoldenFlights.WithoutTheFrame(s); GoldenFlights.WithoutTheHangingGive(s); GoldenFlights.WithoutTheRest(s); GoldenFlights.WithoutTheBells(s); s.Contacts.BladeRoot = false; s.Settings.airmode = true; GoldenFlights.CleanSensors(s); GoldenFlights.Edgewise(s, false, LogFormat.StepSize, false); GoldenFlights.WithoutTheReach(s); }); // since model 60 without the skid's frame, since 63 the bells, since 64 a blade at any radius (ContactSpec.BladeRoot off: a blade meets the bark at its hub on the way), since 69 airmode on (D-086), since 72 the clean sensors, since 79 without the edgewise thrust (76's), since 86 without the anchors' reach
            foreach (string line in p.Order) TestContext.Out.WriteLine("  " + line);
            Assert.That(p.Tunnels, Is.EqualTo(1));
            Assert.That(p.TunnelThrough[0], Is.EqualTo("skid_left through V01_L bark"));
            Assert.That(p.Order, Does.Contain("step 10909: LegBroken leg_fl (Bending, 4.05 past 3.26)"));
            Assert.That(p.Order, Does.Contain("step 10975: Tunnel skid_left (support_fl_end, 0.115 m deep at 2.31 m/s)"), "model 50's Tunnel");
        }

        // Model 20, the wind over a hole: where SampleGround reports Hole the field reads the soil a ray straight down meets
        // (FlightWorld.SoilUnder): the top of the object holding that soil, past any other object's shapes. Over S04's straw
        // mat (its top at y 0, the plane's height) the field blows as over the stub's floor at y 0, over the bare soil between
        // its straw boxes from the soil (y −0.125), and over E01's run from its floor (y −1.5). The known-wrongs: the old
        // rule (a hole's −∞ taken as sheltered) blows nothing over the patches and the run at any height, so the Calm wind
        // at 1 m over the straw is 0 and the range golden in Calm is its flight in still air, bit for bit; the first soil
        // shape alone reads the straw mat's soil box, 0.125 m under the plane's height, and the wind there is not the floor's.
        [Test]
        public void OverAHoleTheWindReadsTheSoil()
        {
            var w = new FlightWorld(RangeScenarios.World);
            var ray = new Ray[1];
            var hit = new RayHit[1];
            var up = new Double3(0, 1, 0);
            Assert.That(w.SoilUnder(RangeScenarios.Straw + up, ray, hit, out _), Is.EqualTo(0).Within(1e-9), "the straw mat's top");
            Assert.That(w.SoilUnder(RangeScenarios.Soil + up, ray, hit, out _), Is.EqualTo(-0.125).Within(1e-9), "the soil box between the straw boxes");
            Assert.That(w.SoilUnder(RangeScenarios.Run + up * 3, ray, hit, out _), Is.EqualTo(-1.5).Within(1e-9), "the run's floor");
            Assert.That(w.SoilUnder(RangeScenarios.Straw - up * 0.5, ray, hit, out _), Is.EqualTo(double.NegativeInfinity), "under the soil: sheltered");

            // The scan of every patch and the run on a 0.5 m grid at four heights, clear of the footprints' edges (a ray straight
            // down an edge grazes the faces there): ground under every point, past what stands on it.
            var xz = new XZ[1];
            var g = new GroundSample[1];
            int most = 0, points = 0, holes = 0, crossed = 0;
            foreach ((double x0, double z0, double x1, double z1) in new[] { (-140.0, -19.355, 160.0, -7.355), (-140.0, 7.353, 160.0, 19.353), (44.0, -21.15, 79.25, -7.4) })
            {
                for (int ix = 0; ix < (int)((x1 - x0) / 0.5); ix++)
                {
                    for (int iz = 0; iz < (int)((z1 - z0) / 0.5); iz++)
                    {
                        double x = x0 + 0.2 + 0.5 * ix, z = z0 + 0.2 + 0.5 * iz; // off the footprints' edges
                        xz[0] = new XZ(x, z);
                        RangeScenarios.World.SampleGround(xz, g);
                        points++;
                        if ((g[0].Flags & GroundFlags.Hole) == 0) continue;
                        holes++;
                        foreach (double y in new[] { 0.5, 2, 10, 40 })
                        {
                            double ground = w.SoilUnder(new Double3(x, y, z), ray, hit, out int casts);
                            Assert.That(ground, Is.InRange(-1.6, 0.5), $"soil under ({x}, {y}, {z})");
                            most = casts > most ? casts : most;
                            if (casts > 1) crossed++;
                        }
                    }
                }
            }
            TestContext.Out.WriteLine($"{holes} of {points} points over a hole, {crossed} rays cast again past a shape, at most {most} casts (SoilCasts {FlightWorld.SoilCasts})");
            Assert.That(holes, Is.GreaterThan(10000));
            Assert.That(8 * most, Is.LessThanOrEqualTo(FlightWorld.SoilCasts), "SoilCasts holds eight times the scan's most");

            // The field over the straw is the stub floor's at the same point: one step 1 m over the mat in Calm.
            MaterialRow[] m = Materials(out ulong h);
            Double3 WindAt(FlightWorld world, WindSetup wind)
            {
                FlightSetup setup = ContactScenarios.Setup(Spec, m, h, true, RangeScenarios.Straw + up);
                setup.World = world;
                setup.Wind = wind;
                var core = new FlightCore(setup);
                core.Advance(default);
                return core.Trace.Wind;
            }
            Double3 over = WindAt(new FlightWorld(RangeScenarios.World), WindSetup.Of(WindLevel.Calm));
            Double3 floor = WindAt(FlightWorld.Floor(), WindSetup.Of(WindLevel.Calm));
            TestContext.Out.WriteLine($"Calm at 1 m over the straw: {over.Length():F4} m/s (the floor's {floor.Length():F4})");
            Assert.That(over.Length(), Is.GreaterThan(0.1), "the field blows over the hole");
            Assert.That((over - floor).Length(), Is.LessThan(1e-9), "as over the floor at the same height");

            // The range golden's flight in Calm is no longer its flight in still air.
            Assert.That(GoldenFlights.FlyRange(Spec, out _), Is.Not.EqualTo(GoldenFlights.FlyRange(Spec, out _, wind: WindSetup.Of(WindLevel.Still))),
                "Calm and still air differ over S04");
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

        // ---- the log ------------------------------------------------------------------------------------------------------

        // A player's flight on the test range, its log naming the world by the map's id as the app's does (FlightWorlds):
        // the legs-on drone at the track start with the airframe's effects on, armed, a collective ramp to 0.7 over 1.5 s,
        // off the ground and climbing. Recorded on the shared map, it replays bit for bit on the map loaded again from the
        // repository, as tools/replay opens it; a log whose world hash is not the map's is refused before its first step.
        [Test]
        public void ARangeFlightReplaysOnTheMapItNames()
        {
            DroneSpec spec = Spec;
            MaterialRow[] m = Materials(out ulong h);
            ContactScenarios.Site site = Airframe(RangeScenarios.At(RangeScenarios.Start("track")));
            double stand = ContactScenarios.Standing(spec.BuildContacts(spec.Nominal, m, h));
            FlightSetup setup = ContactScenarios.Setup(spec, m, h, true, site.Foot + new Double3(0, stand, 0));
            setup.World = site.Open();
            setup.StartAttitude = site.Attitude();
            var core = new FlightCore(site.Effects(setup, spec));
            LogHeader header = LogHeader.For(setup, core);
            header.WorldId = RangeScenarios.World.MapId;
            header.SetDrone(spec, spec.Nominal);
            var stream = new System.IO.MemoryStream();
            var recorder = new FlightRecorder(stream, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            const int Steps = 4000;
            for (int k = 0; k < Steps; k++)
            {
                var stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 200, Throttle = k < 500 ? 0 : DetMath.Min(0.7, 0.7 * (k - 500) / 1500.0) };
                core.Advance(stick);
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
            }
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            Assert.That(core.State.Position.Y - setup.StartPosition.Y, Is.GreaterThan(0.5), "off the range's ground");
            byte[] log = stream.ToArray();

            FlightWorld Open(string id) => id == "test-range" ? new FlightWorld(MapWorld.Load(RangeScenarios.Root, id)) : null;
            ReplayResult r = FlightReplay.Run(new System.IO.MemoryStream(log), Open, spec.ContentHash, materialsHash: h);
            TestContext.Out.WriteLine(r.Message);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Match), r.Message);
            Assert.That(r.Steps, Is.EqualTo(Steps));
            Assert.That(r.Events, Is.GreaterThanOrEqualTo(8), "the supports' contacts broken at liftoff, replayed bit for bit");

            var hash = new byte[8];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(hash, header.WorldHash);
            int at = System.MemoryExtensions.IndexOf(new System.ReadOnlySpan<byte>(log, 0, (int)header.Length), new System.ReadOnlySpan<byte>(hash));
            Assert.That(at, Is.GreaterThan(0), "the header holds the world's hash");
            log[at] ^= 1;
            ReplayResult tampered = FlightReplay.Run(new System.IO.MemoryStream(log), Open, spec.ContentHash, materialsHash: h);
            TestContext.Out.WriteLine(tampered.Message);
            Assert.That(tampered.Outcome, Is.EqualTo(ReplayOutcome.Refused), tampered.Message);
            StringAssert.Contains("the world differs: the log was flown on world 'test-range'", tampered.Message);
            Assert.That(tampered.Steps, Is.EqualTo(0), "nothing replayed");
        }
    }
}
