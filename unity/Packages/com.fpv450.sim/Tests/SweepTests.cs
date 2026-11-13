using System;
using System.Collections.Generic;
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
