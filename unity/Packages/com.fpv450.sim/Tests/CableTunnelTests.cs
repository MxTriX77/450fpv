using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// w9-village-physics 2.6d, parts through a wire's cable at speed, on made states (the band's flights are RangeContactTests'):
    /// - a latched pair on a round member reads the member's own surface along its latched normal (RoundMember, ContactSpec.RollOff),
    ///   and a part whose core leaves the member's side has rolled off it: an ordinary break, never a Tunnel, never pressed while
    ///   clear. The known-wrong is model 100's tangent plane, infinite: it pressed a part that had left the member and logged a Tunnel
    ///   when the member left the margin (fault 2, an arm under L03's conductor);
    /// - a touch on an intact leg's tube in its clamp's jaws meets the clamp's mount, as the broken leg's stub does
    ///   (ContactSpec.ClampedTube); the known-wrong is the tube's own 6.6 kN/m there;
    /// - the header records the three switches (with SupportByDepth, whose case is the band's seed 1 at 5.42 m).
    /// </summary>
    public sealed class CableTunnelTests
    {
        static void Print(string s) => TestContext.Progress.WriteLine(s);

        static readonly Double3 X = new Double3(1, 0, 0), Y = new Double3(0, 1, 0), Z = new Double3(0, 0, 1);

        static ShapeGeometry Rod(ShapeKind kind, Double3 centre, double radius, double half) =>
            new ShapeGeometry { Kind = kind, Center = centre, AxisX = Y, AxisY = X, AxisZ = -Z, Radius = radius, HalfLength = half };

        static MemberReading Read(in ShapeGeometry g, Double3[] chain, Double3 a, Double3 b, double r, Double3 n, out double depth, out Double3 point)
        {
            depth = double.NaN;
            point = default;
            return RoundMember.Read(g, chain ?? Array.Empty<Double3>(), a, b, r, n, ref depth, ref point);
        }

        // ---- The law on made states, by hand ------------------------------------------------------------------------------

        // A part (a support's sphere r 5 mm, or an arm's edge capsule r 3 mm lying along the member) under a member R 4.5 mm along x at
        // the origin, latched with n = −y (the member above it): its core h along n from the axis (h = ρ at the touch) and w across it
        // (along z). By hand the depth along n is √(ρ² − w²) − h while |w| < ρ: the plane's ρ − h at w = 0, through the axis and past it;
        // past ρ across, the part is beside the member, its clearance √(h² + w²) − ρ. An arm crossing the member square to it is always
        // under it (its point nearest the axis has w 0): the plane's depth.
        [TestCase(ShapeKind.Capsule)]
        [TestCase(ShapeKind.Cylinder)]
        [TestCase(ShapeKind.Wire)]
        [TestCase(ShapeKind.Sphere)]
        public void TheDepthIsTheMembersOwnAlongTheLatchedNormal(ShapeKind kind)
        {
            const double R = 0.0045;
            ShapeGeometry g = Rod(kind, default, R, 0.5);
            // A chain sagging 0.2 mm over its outer segments; the part meets the middle one, its axis at y 0.
            Double3[] chain = kind == ShapeKind.Wire ? new[] { new Double3(-0.6, 0.0002, 0), new Double3(-0.1, 0, 0), new Double3(0.4, 0, 0), new Double3(0.9, 0.0002, 0) } : null;
            Double3 n = -Y;
            foreach ((double r, bool capsule) in new[] { (0.005, false), (0.003, true) })
            {
                double rho = R + r;
                foreach ((double h, double w) in new[] { (rho, 0.0), (0.004, 0.0), (0.0, 0.0), (-0.0075, 0.0), (-0.012, 0.0), (0.002, 0.003), (-0.006, 0.007), (-0.006, rho * 0.999) })
                {
                    Double3 at = n * h + Z * w, along = capsule ? new Double3(0.06, 0, 0) : default;
                    MemberReading m = Read(g, chain, at - along, at + along, r, n, out double depth, out Double3 point);
                    double hand = Math.Sqrt(rho * rho - w * w) - h;
                    Assert.That(m, Is.EqualTo(MemberReading.Along), $"{kind} r {r} h {h} w {w}");
                    Assert.That(depth, Is.EqualTo(hand).Within(1e-12), $"{kind} r {r} h {h} w {w}: the depth along n");
                    Assert.That(Math.Abs(Double3.Dot(point - at, n) - (depth - r)), Is.LessThan(1e-12), "the point under the core, on the member along n");
                    if (!capsule || kind == ShapeKind.Sphere) continue;
                    // The arm crossing square (along z), its middle w across: its point nearest the axis is under it.
                    MemberReading x = Read(g, chain, at - Z * 0.06, at + Z * 0.06, r, n, out double crossing, out _);
                    Assert.That(x, Is.EqualTo(MemberReading.Along));
                    Assert.That(crossing, Is.EqualTo(rho - h).Within(1e-12), $"{kind} crossing at h {h}: the plane's");
                }
                foreach (double w in new[] { rho * 1.001, 0.02, 0.09 })
                {
                    Double3 at = n * -0.0075 + Z * w, along = capsule ? new Double3(0.06, 0, 0) : default;
                    MemberReading m = Read(g, chain, at - along, at + along, r, n, out double depth, out _);
                    Assert.That(m, Is.EqualTo(MemberReading.Beside), $"{kind} r {r} w {w}: beside");
                    Assert.That(depth, Is.EqualTo(rho - Math.Sqrt(0.0075 * 0.0075 + w * w)).Within(1e-12), "its clearance from the member, below 0");
                }
            }
        }

        // A cylinder's side is round to its caps and over the rim's quarter circle past them; a cap's or a rim's latched normal (not
        // square to the axis) is a face, and a box is a face: the plane stays.
        [Test]
        public void ACylindersSideIsRoundAndItsCapAFace()
        {
            const double R = 0.009, H = 0.1, r = 0.005;
            ShapeGeometry g = Rod(ShapeKind.Cylinder, default, R, H);
            Double3 n = -Y;
            foreach (double over in new[] { 0.0, 0.002, 0.004, 0.0049 })
            {
                Double3 c = X * (H + over) + n * 0.003;
                MemberReading m = Read(g, null, c, c, r, n, out double depth, out _);
                double rho = R + Math.Sqrt(r * r - over * over);
                Assert.That(m, Is.EqualTo(MemberReading.Along));
                Assert.That(depth, Is.EqualTo(rho - 0.003).Within(1e-12), $"{over * 1e3} mm past the cap: the rim's quarter circle");
            }
            Double3 past = X * (H + r * 1.01) + n * 0.003;
            Assert.That(Read(g, null, past, past, r, n, out double clear, out _), Is.EqualTo(MemberReading.Beside), "past the rim");
            Assert.That(clear, Is.EqualTo(r - r * 1.01).Within(1e-12));
            Double3 tilted = new Double3(0.001, -1, 0) * (1 / Math.Sqrt(1 + 1e-6));
            Assert.That(Read(g, null, default, default, r, tilted, out _, out _), Is.EqualTo(MemberReading.Face), "a cap's or rim's normal: its plane");
            var box = new ShapeGeometry { Kind = ShapeKind.Box, Center = default, AxisX = X, AxisY = Y, AxisZ = Z, HalfExtents = new Double3(0.1, 0.0045, 0.0045) };
            Assert.That(Read(box, null, default, default, r, n, out _, out _), Is.EqualTo(MemberReading.Face), "a box's face: its plane");
        }

        // ---- Fault 2 on a made state: an arm under a conductor ---------------------------------------------------------------

        sealed class Slide
        {
            public int Tunnels, Breaks;
            public double PressedClear;   // the most force on the arm while the conductor is 10 mm or more clear of it, N
            public double DepthSpanning;  // the pair's force while the conductor still crosses the arm, N
            public long Allocated;
        }

        // The drone's front-right arm alone (its two edge capsules, r 3 mm, 18 kN/m out of its plane; every other shape off, Reuse), held
        // level under a rigid conductor (a steel capsule R 4.5 mm along x, 1 mm over the arm's core reach ρ = 7.5 mm, across the arm 0.10 m
        // forward of the centre), then moved by hand: up 16 mm at 2 m/s, so its core stands 7.5 mm past the conductor's axis (fault 2's arm
        // at 11681, bent 16 mm), then forward at 5 m/s until the conductor has passed the arm's root ends and is 0.13 m behind them.
        static Slide SlideTheArm(bool rollOff)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, m, h);
            contacts.RollOff = rollOff;
            FlightWorld world = FlightWorld.Floor();
            world.Query.ResetRuntimeObjects();
            const double R = 0.0045, Lift = 0.0085, Ahead = 0.10, Height = 3, Root = 0.027224; // arm_fr_a's root end, the last the conductor passes
            int conductor = world.Query.AddObject("conductor", new[]
            {
                new ShapeGeometry { Kind = ShapeKind.Capsule, Center = new Double3(0, Height + Lift, Ahead), AxisX = Y, AxisY = X, AxisZ = -Z, Radius = R, HalfLength = 1.0,
                    Material = world.Query.MaterialId("steel") },
            });
            double mass = spec.BuildMass(spec.Nominal).Properties.Mass;
            var model = new ContactModel(contacts, world, mass, random: new FlightRandom(0x450F));
            int[] arm = { Array.FindIndex(contacts.Shapes, x => x.Name == "arm_fr_a"), Array.FindIndex(contacts.Shapes, x => x.Name == "arm_fr_b") };
            model.Reuse(mass, arm, 2, new[] { contacts.Shapes[arm[0]], contacts.Shapes[arm[1]] });
            MassProperties body = default;
            body.Mass = mass;
            var s = new FlightState { Attitude = Quat.Identity, Position = new Double3(0, Height, 0) };
            var events = new StepEvents();
            model.Sense(s, ref s, events);
            var r = new Slide();
            for (int k = 0; k < 50; k++)
            {
                FlightState was = s;
                s.Step++;
                s.Velocity = k < 8 ? new Double3(0, 2, 0) : new Double3(0, 0, 5);
                s.Position = k < 8 ? new Double3(0, Height + 0.002 * (k + 1), 0) : new Double3(0, Height + 0.016, 0.005 * (k - 7));
                events.Clear();
                long before = AllocProbe.Before();
                model.Sense(was, ref s, events);
                Double3 f = default, moment = default;
                var trace = new StepTrace();
                model.Forces(ref s, body, ref f, ref moment, ref trace, events);
                if (k >= 10) r.Allocated += AllocProbe.After(before);
                for (int e = 0; e < events.Count; e++)
                {
                    if (events[e].Object != conductor) continue;
                    if (events[e].Kind == EventKind.Tunnel) r.Tunnels++;
                    if (events[e].Kind == EventKind.ContactBroken) r.Breaks++;
                }
                // The conductor behind the arm's last root end, and its clearance from it (the core 7.5 mm over its axis).
                double behind = Root - (Ahead - s.Position.Z), up = s.Position.Y - Height - Lift, clear = Math.Sqrt(behind * behind + up * up) - R - 0.003;
                if (k >= 8 && Ahead - s.Position.Z > 0.040659) r.DepthSpanning = Math.Max(r.DepthSpanning, -f.Y); // it crosses both edges
                if (behind > 0 && clear >= 0.010) r.PressedClear = Math.Max(r.PressedClear, f.Length());
            }
            return r;
        }

        // By hand: pressed 7.5 mm past the axis the arm's depth along n is ρ + 7.5 = 15 mm, its 18.7 kN/m out of its plane (StiffnessUp,
        // the arm level) about 270 N each edge, while the conductor crosses it. When the conductor passes a root end by more than ρ its
        // core is beside the member: the pair ends as a break, and nothing presses the arm. The known-wrong keeps the plane: it presses the
        // arm with its 15 mm while the conductor is 10 mm and more clear of it, and logs a Tunnel when the conductor leaves the 0.1 m margin.
        [Test]
        public void AnArmPressedPastAConductorRollsOffItsRootEnds()
        {
            Slide right = SlideTheArm(true), wrong = SlideTheArm(false);
            foreach ((string name, Slide x) in new[] { ("the member's own surface", right), ("model 100's tangent plane (the known-wrong)", wrong) })
                Print($"{name}: {x.DepthSpanning:F1} N while it crosses the arm, {x.PressedClear:F1} N at 10 mm and more clear, {x.Breaks} breaks, {x.Tunnels} tunnels, "
                    + $"{x.Allocated} B in 40 steps");
            double hand = 2 * Array.Find(RepositoryDrone.Spec().BuildContacts(RepositoryDrone.Spec().Nominal, ContactScenarios.RepositoryMaterials(out ulong h), h).Shapes,
                x => x.Name == "arm_fr_b").StiffnessUp * 0.015;
            Print($"by hand: 2 edges × StiffnessUp × 15 mm = {hand:F1} N");
            Assert.That(right.DepthSpanning, Is.EqualTo(hand).Within(1e-6), "pressed 15 mm along n by the conductor while it crosses the arm (both edges)");
            Assert.That(right.Tunnels, Is.Zero, "rolled off the conductor: no Tunnel");
            Assert.That(right.Breaks, Is.EqualTo(2), "each edge's pair ends as a break");
            Assert.That(right.PressedClear, Is.Zero, "nothing presses an arm the conductor has left");
            Assert.That(right.Allocated, Is.Zero, "0 B a step" + AllocProbe.Report());
            Assert.That(wrong.DepthSpanning, Is.EqualTo(right.DepthSpanning).Within(1e-9), "the plane's while the conductor crosses the arm");
            Assert.That(wrong.PressedClear, Is.GreaterThan(200), "the known-wrong presses an arm the conductor has left");
            Assert.That(wrong.Tunnels, Is.GreaterThan(0), "and logs a Tunnel when the conductor leaves the margin");
        }

        // ---- A touch in a leg's jaws -----------------------------------------------------------------------------------------

        // The drone's leg_fl tube alone (r 5 mm; every other shape off, Reuse), level, a steel rod (R 4.5 mm along x) 1 mm in front of
        // it at a height, then the drone placed 2 mm into it and held, at rest: the tube pressed 1 mm. Its force back along z and the
        // legs' law's events.
        static double PressTheTube(double height, bool clamped, out int legEvents)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, m, h);
            contacts.ClampedTube = clamped;
            FlightWorld world = FlightWorld.Floor();
            world.Query.ResetRuntimeObjects();
            const double R = 0.0045, Lift = 3;
            int tube = Array.FindIndex(contacts.Shapes, x => x.Name == "leg_fl");
            ContactShape t = contacts.Shapes[tube];
            world.Query.AddObject("rod", new[]
            {
                new ShapeGeometry { Kind = ShapeKind.Capsule, Center = new Double3(t.A.X, Lift + height, t.A.Z + t.Radius + R + 0.001), AxisX = Y, AxisY = X, AxisZ = -Z,
                    Radius = R, HalfLength = 0.05, Material = world.Query.MaterialId("steel") },
            });
            double mass = spec.BuildMass(spec.Nominal).Properties.Mass;
            var model = new ContactModel(contacts, world, mass, random: new FlightRandom(0x450F));
            model.Reuse(mass, new[] { tube }, 1, new[] { t });
            MassProperties body = default;
            body.Mass = mass;
            var s = new FlightState { Attitude = Quat.Identity, Position = new Double3(0, Lift, 0) };
            var events = new StepEvents();
            model.Sense(s, ref s, events);
            Double3 f = default;
            legEvents = 0;
            for (int k = 0; k < 20; k++)
            {
                FlightState was = s;
                s.Step++;
                s.Position = new Double3(0, Lift, 0.002); // pressed quasi-statically: no closing speed for the dampers
                events.Clear();
                model.Sense(was, ref s, events);
                f = default;
                Double3 moment = default;
                var trace = new StepTrace();
                model.Forces(ref s, body, ref f, ref moment, ref trace, events);
                for (int e = 0; e < events.Count; e++)
                    if (events[e].Kind >= EventKind.LegSet && events[e].Kind <= EventKind.SkidLoose) legEvents++;
            }
            return -f.Z;
        }

        // By hand: in the jaws (the clamp_fl block spans the tube's top 16.5 mm; its bottom edge, LegDamageSpec.Clamp, 11.5 mm under
        // the tube's top end) a horizontal push meets the plate arm under the clamp in its plane, StubStiffness 1.172 MN/m (derived):
        // 1172 N on 1 mm. Under the jaws it meets the tube's own penetration spring, 6.6 kN/m (assumed): 6.6 N, and bends the tube at
        // the edge by its height under it, far under its first yield. The known-wrong meets 6.6 kN/m in the jaws too: the conductor
        // went 123 mm through leg_fl's jaws at 10.3 m/s (the band, seed 3, 5.58 m).
        [Test]
        public void ATouchInALegsJawsMeetsTheClampsMount()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, ContactScenarios.RepositoryMaterials(out ulong h), h);
            ContactShape t = Array.Find(contacts.Shapes, x => x.Name == "leg_fl");
            double edge = contacts.Legs.Clamp[3].Y, inJaws = (t.A.Y + edge) / 2, under = edge - 0.05;
            Assert.That(contacts.Legs.Clamp[3].X, Is.EqualTo(t.A.X).And.Not.EqualTo(0), "leg_fl's clamp (Corners order: fl last)");
            double jaws = PressTheTube(inJaws, true, out int e1), free = PressTheTube(under, true, out int e2), wrong = PressTheTube(inJaws, false, out int e3);
            Print($"in the jaws ({inJaws * 1e3:F2} mm, the edge at {edge * 1e3:F2}): {jaws:F2} N (StubStiffness {contacts.Legs.StubStiffness:F0} N/m); "
                + $"under them: {free:F3} N (the tube's {t.Stiffness:F0} N/m); the known-wrong in the jaws: {wrong:F3} N; leg events {e1}, {e2}, {e3}");
            Assert.That(jaws, Is.EqualTo(contacts.Legs.StubStiffness * 0.001).Within(1e-6 * contacts.Legs.StubStiffness), "the clamp's mount in its plane");
            Assert.That(free, Is.EqualTo(t.Stiffness * 0.001).Within(1e-9 * t.Stiffness), "the tube's own spring under the jaws");
            Assert.That(wrong, Is.EqualTo(t.Stiffness * 0.001).Within(1e-9 * t.Stiffness), "the known-wrong: the tube's spring in the jaws");
            Assert.That(e1 + e2, Is.Zero, "nothing sets, cracks or breaks");
        }

        // ---- The header ----------------------------------------------------------------------------------------------------

        // The three switches are recorded in their own header entry and read back, on and off, so a flight flown with a known-wrong
        // replays.
        [Test]
        public void TheHeaderRecordsTheSwitches()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            foreach (bool on in new[] { true, false })
            {
                FlightSetup setup = ContactScenarios.Setup(spec, m, h, true, new Double3(0, 1, 0));
                setup.Contacts.RollOff = setup.Contacts.SupportByDepth = setup.Contacts.ClampedTube = on;
                var core = new FlightCore(setup);
                Log.LogHeader back = Log.LogHeader.Read(new System.IO.MemoryStream(Log.LogHeader.For(setup, core).Encode()));
                Assert.That(back.Unknown, Is.Empty);
                Assert.That((back.Contacts.RollOff, back.Contacts.SupportByDepth, back.Contacts.ClampedTube), Is.EqualTo((on, on, on)));
            }
        }
    }
}
