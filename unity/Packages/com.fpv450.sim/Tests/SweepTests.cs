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
