using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // ground-contact (p6-flight-core 1.6) on the world-query stub: drone.json's contact shapes, physics_materials.json,
    // the scenarios (ContactScenarios), and the log's contacts and runtime objects through a replay.
    public sealed class ContactTests
    {
        static MaterialRow[] Materials(out ulong hash) => ContactScenarios.RepositoryMaterials(out hash);

        // ---- drone.json's contacts block ---------------------------------------------------------------------------------

        [Test]
        public void TheDroneCarriesItsContactShapes()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            Assert.That(spec.Contacts, Is.Not.Null);
            var roles = new Dictionary<ContactRole, int>();
            foreach (ContactShape s in spec.Contacts.Shapes) roles[s.Role] = roles.TryGetValue(s.Role, out int n) ? n + 1 : 1;
            Assert.That(roles[ContactRole.Support], Is.EqualTo(8), "two spheres a leg: the overhang end and the T-joint (P3)");
            Assert.That(roles[ContactRole.Skid], Is.EqualTo(4));
            Assert.That(roles[ContactRole.Disc], Is.EqualTo(4));
            Assert.That(spec.Contacts.Margin, Is.EqualTo(0.1));
            foreach (ContactShape s in spec.Contacts.Shapes)
            {
                if (s.Role == ContactRole.Support)
                {
                    Assert.That(s.A.Y - s.Radius, Is.EqualTo(-0.253).Within(1e-12), s.Name + ": the skid's bottom (p5-drone 1.2)");
                    Assert.That(s.Material, Is.EqualTo("carbon_composite"));
                }
                if (s.Name == "arm_rr_a")
                {
                    Assert.That(s.Radius, Is.EqualTo(0.003), "the 6 mm plate's half-thickness (P5)");
                    Assert.That(s.StiffnessUp, Is.EqualTo(18000), "1.8e4 N/m out of its plane at the post (P5)");
                    Assert.That(s.Stiffness, Is.GreaterThan(s.StiffnessUp), "a plate is stiffer in its plane");
                }
            }
        }

        [Test]
        public void ShapesFollowTheirPartsThroughTheLoadout()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] m = Materials(out ulong h);
            ContactSpec on = spec.BuildContacts(spec.Nominal, m, h);
            Loadout offLoadout = spec.Nominal;
            offLoadout.Legs = false;
            ContactSpec off = spec.BuildContacts(offLoadout, m, h);
            Assert.That(on.Shapes.Length, Is.EqualTo(spec.Contacts.Shapes.Length));
            foreach (ContactShape s in off.Shapes)
                Assert.That(s.Role == ContactRole.Support || s.Role == ContactRole.Skid || s.Name.StartsWith("leg_"), Is.False, s.Name + " comes off with the legs");
            Assert.That(off.Shapes.Length, Is.EqualTo(on.Shapes.Length - 16));
            ContactShape coilOn = Array.Find(on.Shapes, s => s.Name == "coil"), coilOff = Array.Find(off.Shapes, s => s.Name == "coil");
            Assert.That(coilOn.A.Y, Is.EqualTo(0.087));
            Assert.That(coilOff.A.Y, Is.EqualTo(-0.053).Within(1e-12), "legs off, the spool hangs under the plate (D-043)");
            Assert.That(coilOff.A.Z - coilOff.B.Z, Is.EqualTo(coilOn.A.Z - coilOn.B.Z).Within(1e-12));
            Assert.That(LaunchStand.RestingOrigin(spec, off, default).Y, Is.EqualTo(0.253), "the arms' undersides on the 0.25 m posts");

            Loadout none = spec.Nominal;
            none.Payload = 0;
            Assert.That(Array.Exists(spec.BuildContacts(none, m, h).Shapes, s => s.Name.StartsWith("payload")), Is.False, "no payload, no payload shapes");
            Loadout two = spec.Nominal;
            two.Payload = 2;
            ContactShape big = Array.Find(spec.BuildContacts(two, m, h).Shapes, s => s.Name == "payload");
            ContactShape one = Array.Find(on.Shapes, s => s.Name == "payload");
            Assert.That(big.Radius / one.Radius, Is.EqualTo(Math.Pow(2, 1.0 / 3)).Within(1e-12), "the payload's shapes scale with it (DRN-03)");
            Assert.That(big.A.Y + big.Radius, Is.EqualTo(one.A.Y + one.Radius).Within(1e-12), "its top stays put");
        }

        [TestCase("\"radius_m\": {\"value\": 0.005, \"label\": \"measured\", \"note\": \"legs.foot_radius_m\"}", "\"radius_m\": {\"value\": 0.006, \"label\": \"measured\"}", "legs.foot_radius_m")]
        [TestCase("\"role\": \"disc\", \"corner\": \"rr\"", "\"role\": \"disc\", \"corner\": \"fr\"", "motor's position_m")]
        [TestCase("\"stiffness_up_n_per_m\": {\"value\": 18000", "\"stiffness_upward\": {\"value\": 18000", "not a field of the schema")]
        [TestCase("\"role\": \"skid\"", "\"role\": \"sled\"", "is not one of")]
        public void AFaultyContactsBlockIsRefused(string from, string to, string expected)
        {
            string text = RepositoryDrone.Text();
            int at = text.IndexOf(from, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), "the fixture text is in drone.json");
            string broken = text.Substring(0, at) + to + text.Substring(at + from.Length);
            var e = Assert.Throws<DroneSpecException>(() => DroneSpec.Parse(broken));
            StringAssert.Contains(expected, e.Message);
        }

        [Test]
        public void TheBootstrapFixtureHasNoContacts() => Assert.That(BootstrapDrone.Spec().Contacts, Is.Null);

        // ---- physics_materials.json -------------------------------------------------------------------------------------

        [Test]
        public void ThePhysicsMaterialsAreRead()
        {
            MaterialRow[] rows = Materials(out ulong hash);
            Assert.That(hash, Is.Not.EqualTo(0UL));
            var steel = Array.Find(rows, r => r.Name == "steel");
            var sheet = Array.Find(rows, r => r.Name == "sheet_metal");
            Assert.That(steel.Rigid, Is.True, "steel is rigid per part");
            Assert.That(steel.StaticFriction, Is.EqualTo(0.35));
            Assert.That(steel.KineticFriction, Is.EqualTo(0.28));
            Assert.That(sheet.Rigid, Is.False);
            Assert.That(sheet.Stiffness, Is.EqualTo(20000));
            Assert.That(sheet.DampingRatio, Is.EqualTo(0.03));
            Assert.That(Array.Exists(rows, r => r.Name == "timber" && r.Rigid), Is.True);
            Assert.Throws<FormatException>(() => PhysicsMaterials.Parse("{\"materials\": {\"x\": {\"friction_static\": 0.2, \"friction_kinetic\": 0.3}}}", out _));
            Assert.Throws<FormatException>(() => PhysicsMaterials.Parse("{\"materials\": {\"x\": {\"friction_static\": 0.4, \"friction_kinetic\": 0.3, \"stiffness_n_per_m\": 100}}}", out _));
        }

        [Test]
        public void AWorldMaterialWithoutARowIsRefused()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] m = Materials(out ulong h);
            var setup = ContactScenarios.Setup(spec, m, h, true, new Double3(0, 1, 0));
            setup.World = new FlightWorld(new FloorWorld(0, "belt_bare", "steel", "unobtainium"));
            var e = Assert.Throws<ArgumentException>(() => new FlightCore(setup));
            StringAssert.Contains("unobtainium", e.Message);
        }

        // ---- the scenarios ----------------------------------------------------------------------------------------------

        [Test]
        public void HoldTheSticksAndItLeavesTheGround()
        {
            MaterialRow[] m = Materials(out ulong h);
            ContactScenarios.Landing held = ContactScenarios.Land(RepositoryDrone.Spec(), m, h, false);
            ContactScenarios.Landing cut = ContactScenarios.Land(RepositoryDrone.Spec(), m, h, true);
            TestContext.Out.WriteLine($"held: touch {held.TouchSpeed:F3} m/s, left at {held.LeaveSpeed:F3} m/s, rose {held.SeparationHeight * 1e3:F1} mm; "
                + $"cut: rose {cut.SeparationHeight * 1e3:F1} mm, at rest {cut.Settled:F3} s after the touch");
            Assert.That(held.TouchSpeed, Is.EqualTo(0.5).Within(0.05), "the slow glide path's 0.5 m/s");
            Assert.That(held.Separated, Is.True, "held: the legs throw it back into the air (manifesto §2.6)");
            Assert.That(held.SeparationHeight, Is.GreaterThan(0.05), "and visibly so");
            Assert.That(double.IsNaN(held.Settled), Is.True, "held, it is not at rest 4 s later");
            Assert.That(cut.Settled, Is.LessThanOrEqualTo(2), "cut: it stays down within 2 s");
            Assert.That(cut.SeparationHeight, Is.LessThan(held.SeparationHeight / 10), "cut: at most a hop");
            foreach (ContactScenarios.Landing r in new[] { held, cut })
                Assert.That(r.Tunnels + r.Crashes + r.Lost, Is.EqualTo(0));
        }

        [Test]
        public void LiftoffFromTheStand()
        {
            MaterialRow[] m = Materials(out ulong h);
            ContactScenarios.Liftoff r = ContactScenarios.FromStand(RepositoryDrone.Spec(), m, h);
            TestContext.Out.WriteLine($"{r.Made} made, {r.Broken} broken, off at throttle {r.Throttle:F3} (duty {r.Duty:F3}) {r.Time:F3} s into the ramp");
            Assert.That(r.MadeSteel, Is.EqualTo(8), "each arm's two edges on its post, in steel");
            Assert.That(r.RestDepth, Is.EqualTo(4.716 * 9.80665 / (8 * 18000)).Within(2e-6), "resting m·g/(8·k_up) deep: the arms' out-of-plane give");
            Assert.That(r.BrokenSteel, Is.EqualTo(r.Broken));
            Assert.That(r.Broken, Is.GreaterThanOrEqualTo(8), "it leaves all four posts");
            Assert.That(r.Duty, Is.InRange(0.45, 0.65), "near hover's duty (1.4: 47–49 % at L4)");
            Assert.That(r.ClimbAfter, Is.GreaterThan(0.05));
            Assert.That(r.Slide, Is.LessThan(0.005), "it does not slide off a post on the way");
            Assert.That(r.Tunnels + r.Crashes + r.Lost, Is.EqualTo(0));
        }

        [Test]
        public void IntoAPole()
        {
            MaterialRow[] m = Materials(out ulong h);
            ContactScenarios.PoleStrike r = ContactScenarios.IntoPole(RepositoryDrone.Spec(), m, h);
            TestContext.Out.WriteLine($"crash ({r.Cause}) by {r.Part} on {r.Object} ({r.Material}) at {r.Speed:F2} m/s flying {r.FlightSpeed:F2} m/s; "
                + $"peak {r.PeakAccel:F0} m/s², impulse {r.Impulse:F2} N·s; {r.Tunnels} tunnels");
            Assert.That(r.FlightSpeed, Is.EqualTo(10).Within(1), "at 10 m/s");
            Assert.That(r.Object, Is.EqualTo("L01_straight"));
            Assert.That(r.Material, Is.EqualTo("timber"));
            Assert.That(r.Part, Is.Not.Null.And.Not.Empty, "named by the part that hit");
            Assert.That(r.PeakAccel, Is.GreaterThan(490.3), "past the 50 g threshold");
            Assert.That(r.Impulse, Is.GreaterThan(4.8 * 5), "most of its momentum");
            Assert.That(r.Tunnels, Is.EqualTo(0), "no tunnel");
            Assert.That(r.Lost, Is.EqualTo(0));
        }

        [Test]
        public void ATunnelIsLoggedAsABug()
        {
            MaterialRow[] m = Materials(out ulong h);
            Assert.That(ContactScenarios.Overdriven(RepositoryDrone.Spec(), m, h, 2, out string slow), Is.EqualTo(0), slow);
            Assert.That(ContactScenarios.Overdriven(RepositoryDrone.Spec(), m, h, 40, out string fast), Is.GreaterThanOrEqualTo(1), fast);
        }

        // ---- the log -------------------------------------------------------------------------------------------------------

        // The stand's liftoff, logged through the recorder: its header holds the contacts and the stand, and a replay that
        // opens a bare floor places the stand again from it and matches bit for bit.
        static byte[] StandFlight(out LogHeader header, int steps = 4000)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] m = Materials(out ulong h);
            Loadout off = spec.Nominal;
            off.Legs = false;
            ContactSpec contacts = spec.BuildContacts(off, m, h);
            FlightSetup setup = ContactScenarios.Setup(spec, m, h, false, LaunchStand.RestingOrigin(spec, contacts, default));
            setup.RuntimeObjects = new[] { LaunchStand.Build(spec, default, 0, setup.World.Query.MaterialId("steel")) };
            var core = new FlightCore(setup);
            header = LogHeader.For(setup, core);
            header.WorldId = "floor";
            header.SetDrone(spec, off);
            var stream = new MemoryStream();
            var recorder = new FlightRecorder(stream, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            for (int k = 0; k < steps; k++)
            {
                var stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 8, Throttle = k < 500 ? 0 : DetMath.Min(0.7, 0.7 * (k - 500) / 2500.0) };
                core.Advance(stick);
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
            }
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            return stream.ToArray();
        }

        static FlightWorld OpenWorld(string id) => id == "floor" ? FlightWorld.Floor() : null;

        [Test]
        public void TheStandAndTheContactsReplayFromTheLog()
        {
            byte[] log = StandFlight(out LogHeader written);
            var reader = new FlightLogReader(new MemoryStream(log));
            LogHeader h = reader.Header;
            Assert.That(h.Contacts, Is.Not.Null);
            Assert.That(h.Contacts.Shapes.Length, Is.EqualTo(written.Contacts.Shapes.Length));
            Assert.That(h.Contacts.MaterialsHash, Is.EqualTo(written.Contacts.MaterialsHash));
            Assert.That(h.Contacts.Shapes[5].Name, Is.EqualTo(written.Contacts.Shapes[5].Name));
            Assert.That(h.RuntimeObjects.Length, Is.EqualTo(1));
            Assert.That(h.RuntimeObjects[0].Name, Is.EqualTo(LaunchStand.Name));
            Assert.That(h.RuntimeIds[0], Is.EqualTo(0));
            Assert.That(h.RuntimeObjects[0].Shapes[2].Center.X, Is.EqualTo(written.RuntimeObjects[0].Shapes[2].Center.X));

            ReplayResult r = FlightReplay.Run(new MemoryStream(log), OpenWorld, RepositoryDrone.Spec().ContentHash);
            TestContext.Out.WriteLine(r.Message);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Match), r.Message);
            Assert.That(r.Events, Is.GreaterThanOrEqualTo(16), "the posts' contacts made and broken, replayed bit for bit");

            var summary = new StringWriter();
            FlightSummary.Read(new MemoryStream(log)).Print(summary);
            StringAssert.Contains("runtime object 0 'launch_stand'", summary.ToString());
            StringAssert.Contains("leaves 'launch_stand' shape", summary.ToString());
        }

        // QA review-base finding 4: a cut between a step's record and its events is a cut, not a divergence.
        [Test]
        public void ACutBetweenAStepAndItsEventsStillMatches()
        {
            byte[] log = StandFlight(out _);
            // The first step with events after the start: its record block, then its first event block; cut between them.
            var reader = new FlightLogReader(new MemoryStream(log));
            long recordEnd = -1, at = reader.Header.Length;
            int seenStepWithEvents = 0;
            long lastStepEnd = -1;
            while (reader.Next())
            {
                long next = at + LogFormat.BlockHeaderSize + reader.Payload.Length;
                if (reader.Kind == LogFormat.StepBlock) lastStepEnd = next;
                else if (reader.Kind == LogFormat.EventBlock && lastStepEnd > 0 && LogFormat.StepOf(reader.Payload) > 600 && seenStepWithEvents++ == 0)
                    recordEnd = lastStepEnd;
                at = next;
            }
            Assert.That(recordEnd, Is.GreaterThan(0), "the flight makes events after its start");
            var cut = new byte[recordEnd];
            Array.Copy(log, cut, recordEnd);
            ReplayResult r = FlightReplay.Run(new MemoryStream(cut), OpenWorld, RepositoryDrone.Spec().ContentHash);
            TestContext.Out.WriteLine(r.Message + " / " + string.Join(" / ", r.Notes));
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.MatchCut), r.Message);
        }

        [Test]
        public void TheHeaderCoversTheContactTypes()
        {
            Assert.That(Fields(typeof(ContactSpec)), Is.EquivalentTo(LogHeader.ContactFieldsLogged));
            Assert.That(Fields(typeof(ContactShape)), Is.EquivalentTo(LogHeader.ContactShapeFieldsLogged));
            Assert.That(Fields(typeof(MaterialRow)), Is.EquivalentTo(LogHeader.MaterialRowFieldsLogged));
            Assert.That(Fields(typeof(RuntimeObject)), Is.EquivalentTo(LogHeader.RuntimeObjectFieldsLogged));
            Assert.That(Fields(typeof(ShapeGeometry)), Is.EquivalentTo(LogHeader.ShapeGeometryFieldsLogged));
        }

        static string[] Fields(Type t)
        {
            var names = new List<string>();
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) names.Add(f.Name);
            return names.ToArray();
        }
    }
}
