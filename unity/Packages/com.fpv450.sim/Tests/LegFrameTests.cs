using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7g-legs (model 54): the two legs on one skid act as a frame fore and aft (docs/reference-notes/class-legs.md §2). Along
    /// the skid each leg sways with its foot held square by its T-joint, the skid's bending and its pitch on the two legs (the
    /// frame's give 12(1 + β)/(4 + β)·EI/L³ at the foot); across it each is the cantilever (3EI/L³). Each with its known-wrong:
    /// one value both ways (model 51's legs).
    /// </summary>
    public class LegFrameTests
    {
        const double Move = 0.002; // the body's push across its legs, m

        static DroneSpec Spec => RepositoryDrone.Spec();

        // The whole drone's give across its legs, N/m: the body set down level on the floor depth below its unloaded stance, held
        // 500 steps, moved by Move along direction over 400 and held 100 more; the restoring force's change over the move. A leg
        // removed before the first step (remove ≥ 0) leaves its skid half hanging from its sibling.
        static double Give(ContactSpec contacts, FlightWorld world, double mass, Double3 direction, double depth, int remove, out ContactModel model,
            out double normal)
        {
            world.Query.ResetRuntimeObjects();
            model = new ContactModel(contacts, world, mass, random: new FlightRandom(0x450F));
            var events = new StepEvents();
            var s = new FlightState { Attitude = Quat.Identity, Position = new Double3(0, ContactScenarios.Standing(contacts) - depth, 0) };
            model.Sense(s, ref s, events);
            if (remove >= 0)
            {
                model.Legs.Remove(remove);
                model.Restructure(s, events);
            }
            MassProperties body = default;
            body.Mass = mass;
            Double3 before = default;
            normal = 0;
            for (int k = 0; k < 1000; k++)
            {
                FlightState was = s;
                bool moving = k >= 500 && k < 900;
                if (moving) s.Position = s.Position + direction * (Move / 400);
                s.Velocity = moving ? direction * (Move / 400 / FlightCore.Dt) : default;
                s.Step++;
                events.Clear();
                model.Sense(was, ref s, events);
                Double3 f = default, m = default;
                var trace = new StepTrace();
                model.Forces(ref s, body, ref f, ref m, ref trace, events);
                if (k == 499)
                {
                    before = f;
                    normal = f.Y;
                }
                if (k == 999) return -Double3.Dot(f - before, direction) / Move;
            }
            return double.NaN;
        }

        static double Series(double a, double b) => a * b / (a + b);

        // ---- The frame's numbers ------------------------------------------------------------------------------------------

        [Test]
        public void TheFramesNumbersAreDerived()
        {
            DroneSpec spec = Spec;
            LegFrame f = spec.BuildLegFrame();
            LegTube t = spec.Legs.Tube;
            double ei = t.Modulus.Value * t.SecondMoment, l = t.FreeLength;
            double u = l * Math.Sqrt(f.SwayLoad / ei);
            TestContext.Out.WriteLine($"the foot's restraint: the socket's bed {f.Bed:F1}, the skid between its bores {f.Skid:F1}, its pitch on the legs "
                + $"{f.Pitch:F1}, in series {f.Restraint:F2} N·m/rad (β {f.Beta:F3}; the column's own 4EI/L {4 * ei / l:F2})");
            TestContext.Out.WriteLine($"the frame leg: {f.SideStiffness:F1} N/m at the foot (the cantilever's {t.BendingStiffness:F1}, the guided column's "
                + $"{12 * ei / (l * l * l):F1}), sway load {f.SwayLoad:F2} N (π²EI/4L² {Math.PI * Math.PI * ei / (4 * l * l):F2}, π²EI/L² "
                + $"{Math.PI * Math.PI * ei / (l * l):F2}), its lever {f.SwayLoad / f.SideStiffness * 1e3:F1} mm (the cantilever's {Math.PI * Math.PI * l / 12 * 1e3:F1}); "
                + $"the clamp's share {f.TopShare:F4}, the joint's {f.JointShare:F4}, the uniform moment's stiffness {f.SelfStiffness:F3} N·m/rad");
            // Each closed form against its limits and the root's own equation.
            Assert.That(u * Math.Cos(u) + f.Beta * Math.Sin(u), Is.EqualTo(0).Within(1e-12), "the sway load's root");
            Assert.That(f.SideStiffness, Is.GreaterThan(t.BendingStiffness * 2).And.LessThan(12 * ei / (l * l * l)), "between the cantilever and the guided column");
            Assert.That(f.SideStiffness, Is.EqualTo(784.0).Within(0.5), "the frame's give, the derivation's");
            Assert.That(f.SwayLoad, Is.EqualTo(146.6).Within(0.5), "the frame's sway load, the derivation's");
            Assert.That(f.TopShare, Is.EqualTo(0.5612).Within(5e-4), "the clamp's share of the sway moment");
            LegFrame rigid = LegFrame.Of(ei, l, 1e9), free = LegFrame.Of(ei, l, 1e-9);
            Assert.That(rigid.SideStiffness / (12 * ei / (l * l * l)), Is.EqualTo(1).Within(0.02), "a rigid joint: the guided column");
            Assert.That(free.SideStiffness / t.BendingStiffness, Is.EqualTo(1).Within(0.01), "no joint: the cantilever");
            Assert.That(free.SwayLoad / (Math.PI * Math.PI * ei / (4 * l * l)), Is.EqualTo(1).Within(0.01), "no joint: the cantilever's sway load");
        }

        // ---- A skid's legs sway as its frame fore and aft and as cantilevers across -----------------------------------------

        [Test]
        public void ASkidsLegsSwayAsAFrameForeAndAftAndACantileverAcross()
        {
            DroneSpec spec = Spec;
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, m, h), wrong = GoldenFlights.WithoutTheFrameContacts(contacts);
            double mass = spec.BuildMass(spec.Nominal).Properties.Mass, depth = 0.00024;
            Double3 x = new Double3(1, 0, 0), z = new Double3(0, 0, 1);
            FlightWorld floor = SoilScenarios.Floor("belt_straw");
            double across = Give(contacts, floor, mass, x, depth, -1, out ContactModel a, out double n);
            double along = Give(contacts, floor, mass, z, depth, -1, out ContactModel b, out _);
            double wrongAcross = Give(wrong, floor, mass, x, depth, -1, out _, out _);
            double wrongAlong = Give(wrong, floor, mass, z, depth, -1, out _, out _);
            // The closed form at the measured stance: each leg the lever's secant at its share of the weight, k_t − P/γL, in
            // series with the stick anchor (half the leg's spring on the rigid floor).
            double p = n / 4, anchor = contacts.FrictionStiffnessRatio * contacts.LegStiffness;
            double cantilever = 4 * Series(contacts.LegTubeSideStiffness - p / contacts.LegLeverLength, anchor);
            double frame = 4 * Series(contacts.LegFrameSideStiffness - p / contacts.LegFrameLeverLength, anchor);
            double gives = 0;
            for (int c = 0; c < 4; c++)
            {
                Assert.That(b.Framed(c), Is.True, $"corner {c} is its skid's frame");
                gives += b.FrameSideGive(c);
            }
            TestContext.Out.WriteLine($"on the rigid floor at {n:F1} N ({p:F2} N a leg): across {across:F1} N/m (the cantilevers' {cantilever:F1}), "
                + $"along {along:F1} N/m (the frames' {frame:F1}, the corners' gives {gives:F1}); one value both ways: across {wrongAcross:F1}, along {wrongAlong:F1}");
            Assert.That(across / cantilever, Is.EqualTo(1).Within(0.03), "across the skid: the cantilever's give");
            Assert.That(along / frame, Is.EqualTo(1).Within(0.03), "along the skid: the frame's give");
            Assert.That(along / gives, Is.EqualTo(1).Within(0.01), "the corners' frame gives are the drone's");
            Assert.That(across / wrongAcross, Is.EqualTo(1).Within(1e-9), "the frame changes nothing across the skid");
            // Known-wrong: one value both ways sways along the skid as across it, and misses the frame's give by more than half.
            Assert.That(wrongAlong / wrongAcross, Is.EqualTo(1).Within(0.01), "the known-wrong: one value both ways");
            Assert.That(Math.Abs(wrongAlong / frame - 1), Is.GreaterThan(0.5), "the known-wrong misses the frame");

            // On the soil (S04's straw, the series solve and SoilTrial): the same, against the corners' own gives.
            FlightWorld straw = SoilScenarios.Floor("belt_straw");
            ContactSpec soil = spec.BuildContacts(spec.Nominal, m, h, SoilScenarios.Table()), soilWithout = GoldenFlights.WithoutTheFrameContacts(soil);
            double soilAlong = Give(soil, straw, mass, z, 0.003, -1, out ContactModel sb, out double sn);
            double soilAcross = Give(soil, straw, mass, x, 0.003, -1, out ContactModel sa, out _);
            double soilWrong = Give(soilWithout, straw, mass, z, 0.003, -1, out _, out _);
            double soilGives = 0, soilSides = 0;
            for (int c = 0; c < 4; c++)
            {
                soilGives += sb.FrameSideGive(c);
                soilSides += sa.SideGive(c);
            }
            TestContext.Out.WriteLine($"on S04's straw at {sn:F1} N: along {soilAlong:F1} N/m (the corners' frame gives {soilGives:F1}), across {soilAcross:F1} "
                + $"(their side gives {soilSides:F1}); one value both ways along {soilWrong:F1}");
            Assert.That(soilAlong / soilGives, Is.EqualTo(1).Within(0.05), "on the soil, along: the frames' gives");
            Assert.That(soilAcross / soilSides, Is.EqualTo(1).Within(0.05), "on the soil, across: the cantilevers'");
            Assert.That(soilAlong / soilWrong, Is.GreaterThan(2), "on the soil the frame stiffens the sway along the skid");
        }

        // ---- A lost leg frees its sibling -------------------------------------------------------------------------------

        [Test]
        public void ALostLegFreesItsSibling()
        {
            DroneSpec spec = Spec;
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, m, h);
            double mass = spec.BuildMass(spec.Nominal).Properties.Mass;
            FlightWorld floor = SoilScenarios.Floor("belt_straw");
            const int lost = 0, sibling = 1; // leg_rr off; leg_fr on the starboard skid with it
            Give(contacts, floor, mass, new Double3(0, 0, 1), 0.00024, -1, out ContactModel whole, out _);
            Give(contacts, floor, mass, new Double3(0, 0, 1), 0.00024, lost, out ContactModel off, out _);
            Assert.That(contacts.Legs.Sibling[lost], Is.EqualTo(sibling));
            TestContext.Out.WriteLine($"leg_fr along the skid: {whole.FrameSideGive(sibling):F1} N/m with leg_rr on (across {whole.SideGive(sibling):F1}); "
                + $"{off.FrameSideGive(sibling):F1} N/m with it off (across {off.SideGive(sibling):F1}); the port skid's legs {off.FrameSideGive(2):F1} and {off.FrameSideGive(3):F1}");
            // Known-wrong (the intact skid): its legs are a frame, stiffer along the skid than across.
            Assert.That(whole.Framed(sibling), Is.True);
            Assert.That(whole.FrameSideGive(sibling) / whole.SideGive(sibling), Is.GreaterThan(2.5), "with both legs on: the frame");
            // The lost leg breaks the frame: its sibling is a cantilever both ways; the other skid's frame holds.
            Assert.That(off.Legs.Frame(sibling), Is.False, "the law: no frame on a skid that lost a leg");
            Assert.That(off.Framed(sibling), Is.False, "the contacts: leg_fr sways as a cantilever");
            Assert.That(off.FrameSideGive(sibling), Is.EqualTo(off.SideGive(sibling)), "one give both ways");
            Assert.That(off.Framed(2) && off.Framed(3), Is.True, "the port skid's frame holds");
            Assert.That(off.FrameSideGive(2) / off.SideGive(2), Is.GreaterThan(2.5));
        }

        // ---- A frame's leg yields at both ends -------------------------------------------------------------------------

        // A leg's law under a static force across its foot rising from 0 by 0.01 N a step, the leg loaded P along it and free
        // of the ground (no relief: the demand alone): the force at its first crack, at the first event that names its T-joint,
        // and at its parting (NaN for none by 200 N).
        static void Yield(bool frame, Double3 way, out double crack, out double socket, out double parted, out LegMode partMode)
        {
            DroneSpec spec = Spec;
            LegDamageSpec l = spec.BuildLegDamage();
            var law = new LegDamage(l, new FlightRandom(1));
            var events = new StepEvents();
            crack = socket = parted = double.NaN;
            partMode = 0;
            const int leg = 0;
            for (int k = 1; k <= 20000; k++)
            {
                double force = 0.01 * k;
                var loads = new LegLoads
                {
                    LawAxial = 12.7, LawLateral = way * force, AxialStiffness = 52300, LateralStiffness = 260, FrameLateralStiffness = 760, Frame = frame,
                };
                events.Clear();
                law.Step(leg, loads, out _, events, FlightEvent.Of(EventKind.None), 0);
                for (int i = 0; i < events.Count; i++)
                {
                    FlightEvent e = events[i];
                    if (e.Kind == EventKind.LegCracked && double.IsNaN(crack)) crack = force;
                    if (e.Part == l.TJointParts[leg] && double.IsNaN(socket)) socket = force;
                    if (e.Kind == EventKind.LegBroken || e.Kind == EventKind.SkidLoose)
                    {
                        parted = force;
                        partMode = e.Kind == EventKind.SkidLoose ? LegMode.Bending : (LegMode)e.Index;
                    }
                }
                if (!double.IsNaN(parted)) return;
            }
        }

        [Test]
        public void AFramesLegYieldsAtBothEnds()
        {
            DroneSpec spec = Spec;
            LegDamageSpec l = spec.BuildLegDamage();
            var law = new LegDamage(l, new FlightRandom(1));
            double lever = law.Lever(0), mc = law.CrackMoment(0, 12.7);
            double top = (l.FrameTopShare * l.FreeLength + (lever - l.FreeLength) * l.FrameJointShare) / lever;
            Yield(true, new Double3(0, 0, 1), out double crackF, out double socketF, out double partF, out _);
            Yield(false, new Double3(0, 0, 1), out double crackC, out double socketC, out double partC, out _);
            Yield(true, new Double3(1, 0, 0), out double crackX, out double socketX, out double partX, out _);
            TestContext.Out.WriteLine($"along the skid, the frame: cracks at {crackF:F2} N (the clamp's share {top:F4} of {mc:F3} N·m over the {lever * 1e3:F1} mm "
                + $"lever: {mc / (top * lever):F2} N), its socket yields at {socketF:F2} N, it parts at {partF:F2} N; the cantilever (known-wrong): cracks at "
                + $"{crackC:F2} N ({mc / lever:F2}), parts at {partC:F2} N, its socket never; across the skid the frame's leg cracks at {crackX:F2} N, parts at {partX:F2}");
            Assert.That(crackF / (mc / (top * lever)), Is.EqualTo(1).Within(0.005), "the clamp's share of the frame's sway moment cracks it");
            Assert.That(crackC / (mc / lever), Is.EqualTo(1).Within(0.005), "the cantilever's whole moment");
            Assert.That(socketF, Is.GreaterThan(crackF).And.LessThan(partF), "the socket yields too, before the tube parts");
            Assert.That(partF / partC, Is.GreaterThan(1.5), "the frame's leg holds more along the skid");
            Assert.That(double.IsNaN(socketC), Is.True, "the known-wrong: a cantilever never yields at its socket");
            Assert.That(crackX, Is.EqualTo(crackC), "across the skid the frame's leg is the cantilever");
            Assert.That(double.IsNaN(socketX), Is.True);
        }
    }
}
