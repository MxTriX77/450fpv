using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;
using static Fpv450.Sim.Tests.SweepScenarios;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// w9-village-physics 2.6b: a broken leg's skid half hangs from its sibling's bore, a short beam built in there and bearing on the
    /// ground at its tip, and lies in the ground by its contact's own compression alone: its force over its tube stock's contact
    /// spring (ContactSpec.HangingDepth). Its force is its overlap without its give over the chain in series, the cantilever at the
    /// point's own lever (3EI/r³) and the contact spring. Each against its known-wrong, model 98's placement (HangingDepth off).
    /// </summary>
    public sealed class HangingHalfTests
    {
        static DroneSpec Spec => RepositoryDrone.Spec();

        static void Print(string s) => TestContext.Progress.WriteLine(s);

        // ---- A hanging half pressed on the floor ---------------------------------------------------------------------------

        sealed class Press
        {
            public double Force, Depth, Lever, Bend, Contact, Overlap;
            public long Allocated;
        }

        // The drone held still on the floor, rolled to its left and pitched nose down by Tilt each, leg_fl off before the first step,
        // so its half of the left skid hangs from leg_rl's T-joint and its outer end support alone bears on the floor, Overlap into it
        // as the build places it. Held Steps steps; the half's normal force and its end's depth in the floor from the shapes as placed.
        const double Tilt = 6 / 57.29577951308232, PressOverlap = 0.008;
        const int Steps = 200, Leg = 3;

        static Press PressTheHalf(bool depth)
        {
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            ContactSpec contacts = Spec.BuildContacts(Spec.Nominal, m, h);
            contacts.HangingDepth = depth;
            FlightWorld world = FlightWorld.Floor();
            world.Query.ResetRuntimeObjects();
            double mass = Spec.BuildMass(Spec.Nominal).Properties.Mass;
            var model = new ContactModel(contacts, world, mass, random: new FlightRandom(0x450F));
            int end = -1;
            for (int i = 0; i < contacts.Shapes.Length; i++)
                if (contacts.Shapes[i].Corner == Leg && contacts.Shapes[i].Role == ContactRole.Support && (end < 0 || contacts.Shapes[i].A.Z > contacts.Shapes[end].A.Z)) end = i;
            Quat roll = new Quat(0, 0, Math.Sin(Tilt / 2), Math.Cos(Tilt / 2)), pitch = new Quat(Math.Sin(Tilt / 2), 0, 0, Math.Cos(Tilt / 2));
            Quat q = roll * pitch;
            ContactShape tip = contacts.Shapes[end];
            double lowest = q.Rotate(tip.A).Y;
            for (int i = 0; i < contacts.Shapes.Length; i++)
            {
                ContactShape sh = contacts.Shapes[i];
                if (i == end || sh.Role != ContactRole.Support) continue;
                Assert.That(q.Rotate(sh.A).Y - lowest, Is.GreaterThan(PressOverlap + 0.002), $"{sh.Name} stays off the floor: only the half's end bears");
            }
            var s = new FlightState { Attitude = q, Position = new Double3(0, tip.Radius - PressOverlap - lowest, 0) };
            var events = new StepEvents();
            model.Sense(s, ref s, events);
            model.Legs.Remove(Leg);
            model.Restructure(s, events);
            Assert.That((model.Corner(Leg) & CornerState.Hanging) != 0, "leg_fl's half hangs from leg_rl's T-joint");
            MassProperties body = default;
            body.Mass = mass;
            var r = new Press();
            Double3 f = default;
            for (int k = 0; k < Steps; k++)
            {
                FlightState was = s;
                s.Step++;
                events.Clear();
                long before = AllocProbe.Before();
                model.Sense(was, ref s, events);
                Double3 moment = default;
                f = default;
                var trace = new StepTrace();
                model.Forces(ref s, body, ref f, ref moment, ref trace, events);
                if (k >= 10) r.Allocated += AllocProbe.After(before);
            }
            LegDamageSpec legs = contacts.Legs;
            r.Force = f.Y;
            r.Depth = tip.Radius - (s.Position + s.Attitude.Rotate(model.Shape(end).A)).Y;
            r.Lever = (tip.A - legs.Bore[legs.Sibling[Leg]]).Length();
            r.Bend = 3 * legs.Modulus * legs.SecondMoment / (r.Lever * r.Lever * r.Lever);
            r.Contact = Array.Find(contacts.Shapes, x => x.Name == "leg_rr").Stiffness;
            r.Overlap = PressOverlap;
            return r;
        }

        // By hand: the end support 0.3225 m from leg_rl's bore (0.22 + 0.1025, the build's), the skid's EI 0.8694 N·m² (the legs' 10/8
        // mm PVC-U tube stock, E 3.0 GPa), so 3EI/r³ = 77.8 N/m, in series with the tube's contact spring (6600 N/m, assumed): 76.9
        // N/m, 0.77 N on its 10 mm overlap, and the end lies 0.77/6600 = 0.12 mm in the floor. At this tilt the legs' axis is 8.5° off
        // the floor's normal, so the chain across it (the holding leg's tube and the anchor in series) takes 2 % of the push: within
        // 3 %. Model 98's placement read the cantilever at the bores' distance (302.8 N/m): 2.9 N and 0.44 mm, both far off.
        [Test]
        public void AHangingHalfPressedOnTheFloorReadsTheHandCalculation()
        {
            Press right = PressTheHalf(true), wrong = PressTheHalf(false);
            double k = right.Bend * right.Contact / (right.Bend + right.Contact), force = k * right.Overlap, depth = force / right.Contact;
            Print($"by hand: r {right.Lever:F4} m, 3EI/r³ {right.Bend:F1} N/m, the contact spring {right.Contact:F0} N/m, in series {k:F1} N/m: "
                + $"{force:F3} N on {right.Overlap * 1e3:F1} mm, {depth * 1e3:F3} mm in the floor");
            foreach ((string name, Press p) in new[] { ("the depth law", right), ("model 98's placement (the known-wrong)", wrong) })
                Print($"{name}: {p.Force:F3} N ({p.Force / force:F3} of the hand's), the end {p.Depth * 1e3:F3} mm in the floor ({p.Depth / depth:F3}), "
                    + $"{p.Allocated} B in {Steps - 10} steps");
            Assert.That(right.Force, Is.EqualTo(force).Within(0.03 * force), "its force, the hand calculation's");
            Assert.That(right.Depth, Is.EqualTo(depth).Within(0.03 * depth), "its depth, its force over its contact spring");
            Assert.That(right.Allocated, Is.Zero, "0 B a step" + AllocProbe.Report());
            Assert.That(Math.Abs(wrong.Force - force), Is.GreaterThan(0.5 * force), "the known-wrong's force is not the hand's");
            Assert.That(Math.Abs(wrong.Depth - depth), Is.GreaterThan(0.5 * depth), "the known-wrong's depth is not the hand's");
        }

        // ---- A slide with a hanging half -----------------------------------------------------------------------------------

        static Context _farmland, _range;
        static Point[] _farmPoints, _rangePoints;

        static Context Farmland() => _farmland ??= new Context(SlopeScenarios.Farmland(), Spec, WindLevel.Calm);
        static Context Range() => _range ??= new Context(MapWorld.Load(RangeScenarios.Root, "test-range"), Spec, WindLevel.Calm);

        sealed class Series
        {
            public readonly double[] Last = { double.NaN, double.NaN, double.NaN, double.NaN };
            public readonly bool[] First = new bool[4];
            public double Jump, Deepest;
            public long JumpAt;
            public int Steps;
        }

        // A sweep row's slide, replayed bit for bit and judged for 0 B as the sweep does, and each hanging corner's deepest shape in
        // the ground (a surface's, as the sweep's Ground check reads it) step by step: the largest step-to-step change and depth past
        // each corner's first hanging step (in that step its shapes stand where its leg held them, its compression the leg's give).
        static Row Slide(Context c, Point p, bool depth, Series series)
        {
            var found = new StaticContact[32];
            Action<FlightCore> observe = core =>
            {
                ContactModel cm = core.Contacts;
                ref readonly FlightState s = ref core.State;
                for (int k = 0; k < 4; k++)
                {
                    if ((cm.Corner(k) & CornerState.Hanging) == 0)
                    {
                        series.Last[k] = double.NaN;
                        continue;
                    }
                    double deep = 0;
                    for (int i = 0; i < cm.ShapeCount; i++)
                    {
                        ContactShape sh = cm.Shape(i);
                        if (sh.Corner != k || cm.ShapeOff(i) || sh.Role != ContactRole.Support && sh.Role != ContactRole.Skid) continue;
                        var cap = new Capsule(s.Position + s.Attitude.Rotate(sh.A), s.Position + s.Attitude.Rotate(sh.B), sh.Radius);
                        int n = Math.Min(c.Map.StaticContacts(cap, cap, 0, found, QueryFilter.NoWires), found.Length);
                        for (int j = 0; j < n; j++)
                            if (found[j].Surface != 0 && -found[j].Distance > deep) deep = -found[j].Distance;
                    }
                    bool first = double.IsNaN(series.Last[k]);
                    if (!first && !series.First[k])
                    {
                        series.Steps++;
                        series.Deepest = Math.Max(series.Deepest, deep);
                        double jump = Math.Abs(deep - series.Last[k]);
                        if (jump > series.Jump)
                        {
                            series.Jump = jump;
                            series.JumpAt = s.Step;
                        }
                    }
                    series.First[k] = first;
                    series.Last[k] = deep;
                }
            };
            Action<FlightCore> prepare = null;
            if (!depth) prepare = core => core.Contacts.Spec.HangingDepth = false;
            return Fly(c, p, Manoeuvre.Slide, 1, prepare: prepare, replay: depth, observe: observe)[0];
        }

        // The audit's rows (3.0b): farmland #329 Slide, whose half (its leg broken, Hanging, Stub) went 1.6 → 47.5 mm into the tilled
        // ground in one step at 0.35 N when its holder's foot touched the short stems' layer, and range #61 Slide on S04, whose halves
        // went through the soil box under the straw tiles, 20–110 mm in a step. With the depth law each half stays within the sweep's
        // allowance (GroundTolerance: no leg's give, no sink) and moves only as the body does, its give following its holder's set
        // in the step it sets (#329's step 7507: leg rl sets 0.12 rad; without that the end stood 7.3 mm in for a step); each flight
        // replays bit for bit and steps at 0 B. Model 98's placement fails Ground on both.
        [Test]
        public void ASlideWithAHangingHalfKeepsItOutOfTheGround()
        {
            Point farm = Array.Find(_farmPoints ??= Draw(Farmland().Map, 400, 1, out _), q => q.Index == 329);
            Point range = Array.Find(_rangePoints ??= Draw(Range().Map, 400, 1, out _), q => q.Index == 61);
            foreach ((string name, Context c, Point p) in new[] { ("farmland #329", Farmland(), farm), ("range #61", Range(), range) })
            {
                Series rs = new Series(), ws = new Series();
                Row right = Slide(c, p, true, rs), wrong = Slide(c, p, false, ws);
                Print($"{name} Slide, the depth law: {right.Line()}");
                Print($"  {rs.Steps} corner-steps hanging, deepest {rs.Deepest * 1e3:F1} mm, largest step {rs.Jump * 1e3:F1} mm at {rs.JumpAt}");
                Print($"{name} Slide, model 98's placement: {wrong.Line()}");
                Print($"  {ws.Steps} corner-steps hanging, deepest {ws.Deepest * 1e3:F1} mm, largest step {ws.Jump * 1e3:F1} mm at {ws.JumpAt}");
                Assert.That(rs.Steps, Is.GreaterThan(100), $"{name}: a half hangs and is judged");
                Assert.That(right.Failed[(int)Check.Ground], Is.False, $"{name}: the hanging half within its allowance");
                Assert.That(rs.Deepest, Is.LessThan(GroundTolerance), $"{name}: never past GroundTolerance in the ground");
                Assert.That(rs.Jump, Is.LessThan(0.005), $"{name}: it moves step to step as the body does (2 m/s, 2 mm a step)");
                Assert.That(right.Failed[(int)Check.Replay], Is.False, $"{name}: the log replays bit for bit");
                Assert.That(right.Failed[(int)Check.Alloc], Is.False, $"{name}: 0 B a step");
                Assert.That(wrong.Failed[(int)Check.Ground], Is.True, $"{name}: model 98's half lies past its allowance");
                Assert.That(ws.Jump, Is.GreaterThan(0.020), $"{name}: and steps by centimetres");
            }
        }
    }
}
