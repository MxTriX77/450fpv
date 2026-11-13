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

        // Lever (3.0b): range #180 Slide, 1.42 N·m on a net 2.36 N, a 0.602 m "lever" on |ΣF|; by hand its pairs' normals sum to
        // 20.8 N and every point of application lies within 0.38 m: a couple. Judged pair by pair (PairReach) it passes; the
        // known-wrongs (the reach off) are WithoutTheReach_TheSweepFailsTheLever and OnE01sSoilShapesTheReachHoldsTheLever, both
        // still failing on the per-pair reading (76–1184 m and 0.63–82 m).
        [Test]
        public void ACoupleOfLightContactsPassesTheLever()
        {
            Row row = FlyRange(180, Manoeuvre.Slide);
            Assert.That(row.Judged[(int)Check.Lever] && !row.Failed[(int)Check.Lever], Is.True, "#180 Slide's couple has its forces within reach");
            Assert.That(row.Worst[(int)Check.Lever], Is.LessThan(Reach));
        }

        // Tunnel (3.0b): range #425 Slide's support_fl_end is dropped from E01's soil box 15.0 mm in, its corner's soil sunk 12.5
        // mm and the ground's pair beside carrying it: the soil's own give, no passage. #431 Slide's hanging half (its leg gone, so
        // no give) is dropped 37.9 mm into the box with no sink: through it, and the corrected check still fails it.
        [Test]
        public void ATunnelOnSoilIsJudgedByTheSoilsDepth()
        {
            Row soil = FlyRange(425, Manoeuvre.Slide), through = FlyRange(431, Manoeuvre.Slide);
            Assert.That(soil.Failed[(int)Check.Tunnel], Is.False, "#425 Slide: 15 mm in a soil box that sank 12.5 mm");
            Assert.That(through.Failed[(int)Check.Tunnel], Is.True, "#431 Slide: a hanging half 38 mm through the box");
        }

        // Ground (3.0b): a frame part's depth is its own penalty spring's at the force its pairs carry, up to its thickness (range
        // #430 Drop: motor_fr 22.8–26 mm in E01's floor at 2.1–2.2 kN, a 4.9 m/s drop on its side), so it passes. The known-wrong:
        // the frame parts 100 times softer (their springs' Stiffness and StiffnessUp × 0.01), so a part sinks wholly under the
        // surface, and the same drop fails. A hanging half's compression in the trace is its own overlap, which excused it by itself, and its
        // give is in its placement already: range #61 Slide's half lies 96.8 mm in the ground with 2 N behind it, and fails. Since 2.6b's
        // depth law that half lies in by its force over its contact spring (HangingHalfTests), so the check's known-wrong flies model
        // 98's placement (ContactSpec.HangingDepth off): 20.6–110.5 mm in the soil box.
        [Test]
        public void AFramePartAtItsForcePasses_ASoftOneOrAHangingHalfInTheGroundFails()
        {
            Row frame = FlyRange(430, Manoeuvre.Drop);
            Row soft = FlyRange(430, Manoeuvre.Drop, core =>
            {
                ContactShape[] shapes = core.Contacts.Spec.Shapes;
                for (int i = 0; i < shapes.Length; i++)
                    if (shapes[i].Role == ContactRole.Body)
                    {
                        shapes[i].Stiffness *= 0.01;
                        shapes[i].StiffnessUp *= 0.01;
                    }
            });
            Row half = FlyRange(61, Manoeuvre.Slide, core => core.Contacts.Spec.HangingDepth = false);
            Assert.That(frame.Failed[(int)Check.Ground], Is.False, "#430 Drop: the motor's depth is its spring's at 2 kN");
            Assert.That(soft.Failed[(int)Check.Ground], Is.True, "#430 Drop on springs 100 times softer: a part wholly under the surface");
            Assert.That(half.Failed[(int)Check.Ground], Is.True, "#61 Slide: a hanging half deep in the ground");
        }

        // Rest (3.0b): range #131 Drop lies on its back with two props at idle; it holds its place to 1 mm and its attitude to 0.2°
        // while its rate swings ±0.16 rad/s at ~15 Hz: at rest. #405 Drop, on its side, slides 0.49 m/s and turns 1.8 rad/s pushed
        // by its idle props on S04's straw 10 s after the touch: not at rest, and the corrected check still fails it.
        [Test]
        public void AnIdleVibrationIsRest_ASlideIsNot()
        {
            Row still = FlyRange(131, Manoeuvre.Drop), sliding = FlyRange(405, Manoeuvre.Drop);
            Assert.That(still.Failed[(int)Check.Rest], Is.False, "#131 Drop: vibrating in place");
            Assert.That(sliding.Failed[(int)Check.Rest], Is.True, "#405 Drop: still sliding");
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
