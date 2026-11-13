using System;
using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;
using static Fpv450.Sim.Tests.SweepScenarios;

namespace Fpv450.Sim.Tests
{
    // The map-wide landing sweep's own tests (w9-village-physics 3.0, SweepScenarios; flightbench --sweep runs it over a map). Its
    // known-wrong: with the soil anchors' reach off (model 85's slide, GoldenFlights.WithoutTheReach) the sweep's cut landing nose
    // uphill at the audit's farmland slope spots fails the moment's lever, as 2.4's AnchorReachTests does; with the reach on, the
    // same flights pass it. And the sweep is deterministic: the same seed draws the same points and flies the same rows.
    public sealed class SweepTests
    {
        static Context _context;
        static Context Village() => _context ??= new Context(SlopeScenarios.Farmland(), RepositoryDrone.Spec(), WindLevel.Calm);

        static void Print(string s) => TestContext.Progress.WriteLine(s);

        // The audit's farmland slope spots where model 85's anchors ran (design.md 2.4's table: 9.39–29.35°).
        static readonly (double X, double Z)[] Spots = { (313.04, 20.00), (311.19, 50.71), (522.83, 587.58), (523.94, 596.09) };

        static Point Spot(double x, double z)
        {
            Assert.That(Classify(Village().Map, x, z, out Point p), Is.True, $"({x}, {z}) is a landing spot");
            return p;
        }

        static double Uphill(in Point p) => Math.Atan2(-p.Normal.X, -p.Normal.Z) * 57.29577951308232;

        [Test]
        public void WithoutTheReach_TheSweepFailsTheLever()
        {
            var wrongFailed = new List<string>();
            var rightFailed = new List<string>();
            foreach (var (x, z) in Spots)
            {
                Point p = Spot(x, z);
                Row wrong = Fly(Village(), p, Manoeuvre.Cut, 1, Uphill(p), GoldenFlights.WithoutTheReach, replay: false)[0];
                Row right = Fly(Village(), p, Manoeuvre.Cut, 1, Uphill(p), replay: false)[0];
                Print($"({x}, {z}) {p.Surface} {p.Slope:F2}°: model 85's slide lever {wrong.Worst[(int)Check.Lever]:F3} m, the reach {right.Worst[(int)Check.Lever]:F3} m");
                Print("  wrong: " + wrong.Line());
                Print("  right: " + right.Line());
                if (wrong.Failed[(int)Check.Lever]) wrongFailed.Add($"({x}, {z})");
                if (right.Failed[(int)Check.Lever]) rightFailed.Add($"({x}, {z}): {right.Detail}");
            }
            Assert.That(wrongFailed, Is.Not.Empty, "the known-wrong (no reach) fails the sweep's lever at some spot");
            Assert.That(rightFailed, Is.Empty, "with the reach no spot fails the lever");
        }

        // The sweep's finding 2 (docs/reviews/w9-village-physics-sweep.md §4): on the test range's E01, whose floor and ends are
        // soil shapes (objects), the reach applied to the ground's pairs only (2.4), and #440's slide down the 22° end held a foot
        // 4.93 m off (214.2 N·m on 43.4 N), its cut 1.10 m. Since w9-village-physics 2.3 every hold lets a foot go where the leg
        // no longer reaches it (the ground, a soil shape, a rigid shape; a crown's twigs at their own reach): #440 passes the lever.
        // The known-wrong, the reach off (model 85's slide), is flown over every point of the 400-point draw on E01's 22° ends,
        // slide and cut: one flight on one point is not a known-wrong, since whether a held foot runs away turns on how that one
        // touchdown goes (model 92's rotors moved #440's off-reach slide from a 4.93 m runaway to 0.324 m). Over the set, the
        // reach off must fail the lever somewhere and the reach on nowhere. The ends only: on E01's level floor the reach does not
        // act (its levers are the same on and off), and the floor's own lever failure (#420's slide) is not this test's.
        [Test]
        public void OnE01sSoilShapesTheReachHoldsTheLever()
        {
            var range = new Context(MapWorld.Load(RangeScenarios.Root, "test-range"), RepositoryDrone.Spec(), WindLevel.Calm);
            Point[] points = Draw(range.Map, 400, 1, out _);
            Point p440 = Array.Find(points, q => q.Index == 440);
            Assert.That(p440.X, Is.EqualTo(59.38).Within(0.01), "the premise: the sweep's #440 on E01's 22° end");
            Point[] ends = Array.FindAll(points, q => q.Surface == "patch:E01_curving" && q.Slope > 20);
            Assert.That(ends.Length, Is.GreaterThanOrEqualTo(10), "the premise: the draw holds E01's ends");
            var wrongFailed = new List<string>();
            var rightFailed = new List<string>();
            double wrongMost = 0, rightMost = 0;
            foreach (Point p in ends)
                foreach (Manoeuvre m in new[] { Manoeuvre.Slide, Manoeuvre.Cut })
                {
                    Row right = Fly(range, p, m, 1, replay: false)[0];
                    Row wrong = Fly(range, p, m, 1, prepare: GoldenFlights.WithoutTheReach, replay: false)[0];
                    double r = right.Worst[(int)Check.Lever], w = wrong.Worst[(int)Check.Lever];
                    rightMost = Math.Max(rightMost, r);
                    wrongMost = Math.Max(wrongMost, w);
                    Print($"#{p.Index} {m}: the largest lever {r:F3} m, without the reach {w:F3} m");
                    if (right.Failed[(int)Check.Lever]) rightFailed.Add($"#{p.Index} {m}: {right.Detail}");
                    if (wrong.Failed[(int)Check.Lever]) wrongFailed.Add($"#{p.Index} {m}");
                }
            Print($"{ends.Length} points on E01's ends: the largest lever {rightMost:F3} m, without the reach {wrongMost:F3} m");
            Assert.That(rightFailed, Is.Empty, "with the reach no flight on E01's ends fails the lever");
            Assert.That(wrongFailed, Is.Not.Empty, "the known-wrong (no reach) fails the lever somewhere on E01's ends");
        }

        // ---- 3.0b: the checks audited by hand, each corrected check against the rows that misjudged and a known-wrong ----------

        static Context _range;
        static Point[] _rangePoints;
        static Context Range() => _range ??= new Context(MapWorld.Load(RangeScenarios.Root, "test-range"), RepositoryDrone.Spec(), WindLevel.Calm);
        static Point RangePoint(int index) => Array.Find(_rangePoints ??= Draw(Range().Map, 400, 1, out _), q => q.Index == index);

        static Row FlyRange(int point, Manoeuvre m, Action<FlightCore> prepare = null)
        {
            Row row = Fly(Range(), RangePoint(point), m, 1, prepare: prepare, replay: false)[0];
            Print($"#{point} {m}: {row.Line()}");
            return row;
        }

        // Energy (3.0b): the blades' passes kick the body after the rigid body with the rotors' own spin (PropStrikes.Apply), the
        // rotors' work; the old check read it as the contacts' return (range #405 Slide: +0.168 J at its first strike, by hand the
        // passes' share to 1 mJ; #86 Slide +0.098 J; #154 Slide, a blade grinding a rigid face, ±7 mJ a step). Credited since the
        // start up to what the strikes took from the rotors since the start, and gravity's work at the kick's own v̄, the slides
        // pass. The known-wrong: a leg damper that pushes instead of resisting (ContactSpec.LegDamping negated, an active contact)
        // gives the body energy no force paid for, and the same Land fails the corrected check.
        [Test]
        public void TheBladesPassesAreTheRotorsWork_AnActiveLegDamperIsNot()
        {
            foreach (int p in new[] { 405, 86, 154 })
                Assert.That(FlyRange(p, Manoeuvre.Slide).Failed[(int)Check.Energy], Is.False, $"#{p} Slide: the passes' kicks are the rotors' work");
            Row right = FlyRange(0, Manoeuvre.Land);
            Row wrong = FlyRange(0, Manoeuvre.Land, core => core.Contacts.Spec.LegDamping = -core.Contacts.Spec.LegDamping);
            Assert.That(right.Failed[(int)Check.Energy], Is.False, "the right legs take energy");
            Assert.That(wrong.Failed[(int)Check.Energy], Is.True, "a damper that pushes gives energy back: the check fails it");
        }

        // The range's points on a patch (S04's straw over its soil, E01's soil boxes), in the draw's order.
        static Point[] RangePatch(string patch) => Array.FindAll(_rangePoints ??= Draw(Range().Map, 400, 1, out _), q => q.Surface == "patch:" + patch);

        // Lever (3.0b): |M|/|ΣF| is no lever where contacts push against each other. Range #180 Slide at model 95 read 0.602 m (1.42
        // N·m on a net 2.36 N), while its pairs' normals summed to 20.8 N and every point of application lay within 0.38 m. Over
        // S04's slides, every step whose |M|/|ΣF| exceeds the farthest point any of its forces acts at (PairReach) is a couple: no
        // force set acting within r of the CoM makes |M| > r·|ΣF| unless its forces partly cancel. Over those steps the per-pair
        // reading stays within Reach whatever |M|/|ΣF| says. The set, not one row: whether a flight's couple passes 0.5 m on |ΣF|
        // turns on its path. The known-wrongs (the reach off) are WithoutTheReach_TheSweepFailsTheLever and
        // OnE01sSoilShapesTheReachHoldsTheLever.
        [Test]
        public void ACouplesLeverIsItsPairsReach()
        {
            int couples = 0;
            double mostRatio = 0, mostReach = 0;
            foreach (Point p in RangePatch("S04_patch"))
                Fly(Range(), p, Manoeuvre.Slide, 1, replay: false, observe: core =>
                {
                    ref readonly StepTrace tr = ref core.Trace;
                    double cf = tr.ContactForce.Length(), cm = tr.ContactMoment.Length();
                    if (!(cf > 0)) return;
                    double far = PairReach.Farthest(core.Contacts, core.State, core.State.Position + core.State.Attitude.Rotate(core.Body.CenterOfMass), out _);
                    if (!(cm > far * cf)) return;
                    couples++;
                    mostRatio = Math.Max(mostRatio, cm / cf);
                    mostReach = Math.Max(mostReach, far);
                });
            Print($"S04's slides: {couples} couple steps, |M|/|ΣF| up to {mostRatio:F3} m, the pairs' farthest point then {mostReach:F3} m");
            Assert.That(couples, Is.GreaterThan(0), "the premise: S04's slides make couples");
            Assert.That(mostReach, Is.LessThan(Reach), "in a couple the pairs' forces act within reach");
        }

        // Tunnel (3.0b), on made events: the core's rule takes a surface's row as rigid, so a part dropped from a soil box inside the
        // soil's own sink and the part's give is no passage (range #425 Slide at model 95: support_fl_end 15.0 mm in E01's box, its
        // soil sunk 12.5 mm), and a real passage through a soil box still fails (#431 Slide at model 95: a hanging half, no give, 37.9
        // mm in with no sink). On anything rigid every Tunnel fails, and every WorldBug.
        [Test]
        public void ATunnelOnSoilIsJudgedByTheSoilsDepth()
        {
            FlightEvent Tunnel(byte surface, double depth)
            {
                FlightEvent e = FlightEvent.Of(EventKind.Tunnel);
                e.Surface = surface;
                e.A = depth;
                return e;
            }
            const byte soil = 4;
            Assert.That(TunnelFails(Tunnel(soil, 0.015), 0, 0.0125, out _), Is.False, "15 mm in a soil box that sank 12.5 mm");
            Assert.That(TunnelFails(Tunnel(soil, 0.0379), LegGive(CornerState.Hanging | CornerState.Stub, 0.0379, 0), 0, out _), Is.True,
                "a hanging half 38 mm through the box, no sink and no give");
            Assert.That(TunnelFails(Tunnel(soil, 0.030), 0.004, 0.0075, out _), Is.False, "a leg's 4 mm and its soil's 7.5 mm hold 30 mm");
            Assert.That(TunnelFails(Tunnel(soil, 0.032), 0.004, 0.0075, out _), Is.True, "and not 32 mm");
            Assert.That(TunnelFails(Tunnel(0, 0.001), 1, 1, out _), Is.True, "through anything rigid, at any depth");
            Assert.That(TunnelFails(FlightEvent.Of(EventKind.WorldBug), 1, 1, out _), Is.True, "a world bug");
            Assert.That(TunnelFails(FlightEvent.Of(EventKind.ContactMade), 0, 0, out _), Is.False, "no other kind");
        }

        // Ground (3.0b): a leg point may lie as deep as its leg's give in the state the step's forces were made in; a half whose leg
        // is gone has none (the trace's compression there is the half's own overlap, which excused it by itself: range #61 Slide at
        // model 95, a half 96.8 mm in with 2 N). A frame part may lie as deep as its own spring's overlap at its force, never past its
        // thickness.
        [Test]
        public void ALegsGiveIsItsLegs_AFramePartsItsSpringsToItsThickness()
        {
            Assert.That(LegGive(CornerState.None, -0.012, 0.030), Is.EqualTo(0.042).Within(1e-12), "a whole leg: along and across");
            Assert.That(LegGive(CornerState.Hanging | CornerState.Stub, 0.0968, 0), Is.EqualTo(0), "a hanging half: none");
            Assert.That(LegGive(CornerState.Hanging | CornerState.Stub | CornerState.Folded, 0.05, 0.02), Is.EqualTo(0), "folded too");
            Assert.That(LegGive(CornerState.Hanging | CornerState.TubeGone, 0.05, 0.02), Is.EqualTo(0), "its tube gone");
            Assert.That(FrameGive(0.026, 0.0186), Is.EqualTo(0.026), "a motor can at 2.2 kN: its spring's 26 mm");
            Assert.That(FrameGive(0.120, 0.0186), Is.EqualTo(0.0372), "never past its 37.2 mm thickness");
        }

        // The same on flights: a frame part's overlap at its own force passes on E01's drops, the soil boxes' hardest landings (at
        // model 95 #430 Drop's motor_fr lay 22.8–26 mm in E01's floor at 2.1–2.2 kN, past the 20 mm tolerance alone), and the
        // known-wrong, the frame parts' springs 1000 times softer on the drops that landed on a frame part, sinks parts wholly under
        // the surface and fails. Over the set, and the known-wrong's set chosen by the right flights: which drop lands on a frame part
        // and how deep turns on its path.
        [Test]
        public void AFramePartAtItsForcePasses_ASoftOneFails()
        {
            Point[] e01 = RangePatch("E01_curving");
            var framed = new List<Point>();
            int frameFails = 0, softFails = 0;
            double deepest = 0;
            var found = new StaticContact[16];
            foreach (Point p in e01)
            {
                double deep = 0;
                Row row = Fly(Range(), p, Manoeuvre.Drop, 1, replay: false, observe: core =>
                {
                    ContactModel cm = core.Contacts;
                    for (int i = 0; i < cm.ShapeCount; i++)
                    {
                        ContactShape sh = cm.Shape(i);
                        if (cm.ShapeOff(i) || sh.Role != ContactRole.Body) continue;
                        var cap = new Capsule(core.State.Position + core.State.Attitude.Rotate(sh.A), core.State.Position + core.State.Attitude.Rotate(sh.B), sh.Radius);
                        int n = Math.Min(Range().Map.StaticContacts(cap, cap, 0, found, QueryFilter.NoWires), found.Length);
                        for (int j = 0; j < n; j++) if (found[j].Surface != 0) deep = Math.Max(deep, -found[j].Distance);
                    }
                })[0];
                deepest = Math.Max(deepest, deep);
                if (deep > GroundTolerance) framed.Add(p);
                if (row.Failed[(int)Check.Ground] && row.Detail.Contains("the corner frame")) frameFails++;
            }
            // The known-wrong on the drops that landed on a frame part (most of E01's drops land on their legs alone).
            foreach (Point p in framed)
            {
                Row row = Fly(Range(), p, Manoeuvre.Drop, 1, prepare: core =>
                {
                    ContactShape[] shapes = core.Contacts.Spec.Shapes;
                    for (int i = 0; i < shapes.Length; i++)
                        if (shapes[i].Role == ContactRole.Body)
                        {
                            shapes[i].Stiffness *= 0.001;
                            shapes[i].StiffnessUp *= 0.001;
                        }
                }, replay: false)[0];
                Print($"soft #{p.Index} Drop: Ground worst {row.Worst[(int)Check.Ground] * 1e3:F1} mm past its allowance, Tunnel worst {row.Worst[(int)Check.Tunnel] * 1e3:F1} mm; {row.Detail}");
                // At model 98 the soft drops lie 46–144 mm past their allowance at worst, the tolerance's 2–7 times.
                if (row.Failed[(int)Check.Ground]) softFails++;
            }
            Print($"E01's {e01.Length} drops: {framed.Count} put a frame part past {GroundTolerance * 1e3:F0} mm (deepest {deepest * 1e3:F1} mm), {frameFails} fail Ground on a frame part; soft springs: {softFails} of {framed.Count} fail");
            Assert.That(framed, Is.Not.Empty, "the premise: E01's drops put frame parts past the tolerance alone");
            Assert.That(frameFails, Is.EqualTo(0), "a frame part's overlap at its own force passes");
            Assert.That(softFails, Is.EqualTo(framed.Count), "the known-wrong: springs 1000 times softer sink a frame part wholly in");
        }

        // Rest (3.0b), on made poses: at rest means the body went nowhere over the hold, its mean speed and turn under the spec's
        // 0.05 m/s and 0.1 rad/s. An idle motor's vibration in place is rest (range #131 Drop at model 95: 1 mm and 0.2° while its
        // rate swung ±0.16 rad/s at ~15 Hz); a slide at 0.49 m/s or a turn at 1.8 rad/s (#405 Drop at model 95, pushed by its idle
        // props on S04's straw) is not; a creep is judged by the spec's own speed.
        [Test]
        public void AnIdleVibrationIsRest_ASlideIsNot()
        {
            int Hold(Func<double, (Double3 At, double Turn)> pose)
            {
                var w = new RestWindow();
                int most = 0;
                for (int k = 0; k < 2 * RestHold; k++)
                {
                    var (at, turn) = pose(k * FlightCore.Dt);
                    most = Math.Max(most, w.Add(at, new Quat(Math.Sin(turn / 2), 0, 0, Math.Cos(turn / 2))));
                }
                return most;
            }
            double w15 = 2 * Math.PI * 15;
            Assert.That(Hold(t => (new Double3(0.001 * Math.Sin(w15 * t), 0, 0), 0.0017 * Math.Sin(w15 * t))), Is.GreaterThanOrEqualTo(RestHold),
                "vibrating 1 mm and 0.1° at 15 Hz (its rate ±0.16 rad/s) is at rest");
            Assert.That(Hold(t => (new Double3(0.49 * t, 0, 0), 0)), Is.LessThan(RestHold), "a slide at 0.49 m/s is not");
            Assert.That(Hold(t => (default, 1.8 * t)), Is.LessThan(RestHold), "a turn at 1.8 rad/s is not");
            Assert.That(Hold(t => (new Double3(0.04 * t, 0, 0), 0.08 * t)), Is.GreaterThanOrEqualTo(RestHold), "a creep under the spec's speeds is at rest");
            Assert.That(Hold(t => (new Double3(0.06 * t, 0, 0), 0)), Is.LessThan(RestHold), "and over them is not");
        }

        [Test]
        public void TheSameSeedGivesTheSameRows()
        {
            MapWorld map = Village().Map;
            Point[] a = Draw(map, 30, 7, out long ca), b = Draw(map, 30, 7, out long cb);
            Assert.That(cb, Is.EqualTo(ca));
            Assert.That(b.Length, Is.EqualTo(a.Length));
            for (int i = 0; i < a.Length; i++)
                Assert.That((b[i].X, b[i].Z, b[i].Surface, b[i].Stems, b[i].Band), Is.EqualTo((a[i].X, a[i].Z, a[i].Surface, a[i].Stems, a[i].Band)));
            string first = Fly(Village(), a[0], Manoeuvre.Drop, 7)[0].Line(), second = Fly(Village(), a[0], Manoeuvre.Drop, 7)[0].Line();
            Print(first);
            Assert.That(second, Is.EqualTo(first));
        }
    }
}
