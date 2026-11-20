using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;

namespace Fpv450.FlightBench
{
    // The pilot's cut table (p7d-tether 1.11i; the pilot, 2026-11-18, D-104): each row's rate of cuts over seeds 1–N against the
    // pilot's own rate, accepted within its binomial spread (two standard deviations at N seeds), red outside it in either
    // direction. The pilot's words: sharp pitch or roll in flight 75 %, about 80 % with high throttle through it; roll alone
    // presses the hole's edge less than pitch, roll with yaw the same way harder (at least pitch's 75 %, the orchestrator's
    // reading); a flip always; a proper brake from top speed 25 %; a very sharp liftoff 87 %, 95 % at max throttle; backwards too
    // much at liftoff with the throttle not yet high about 40 %; normal flying rare (at most about 1 in 10; at least 1 across the
    // set, "sometimes"). Every later fiber change must pass it. The pilot's answer on liftoffs (D-104): more pressure at the
    // liftoff, more cuts; unnecessary roll or pitch backwards 10 points more; throttle with pitch forward, the safest launch, 5 %.
    // The brake graded: a higher-speed proper brake about 7 %, a mild approach rare. The cuts are the line's own mechanics,
    // except at a liftoff, where they are design 26's empirical law of the pilot's account (LiftoffCuts).
    internal static class CutsCase
    {
        sealed class Row
        {
            public string Name, Group;
            public double Rate; // the pilot's rate (NaN: a band given directly)
            public double Low, High;
            public Func<ulong, FiberCutScenarios.Outcome> Fly;
            public FiberCutScenarios.Outcome[] O;
        }

        public static bool Run(int seeds)
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            // A sharp snap's turn: the median of the pilot's own sharp snaps in their 30 logs (fly snaps, 1.11i: 60–273°, median
            // about 110°), at their 225 °/s full-stick rate; a flip 360°.
            const double Sharp = 110, Flip = 360, Rare = 0.1;
            double Spread(double p) => 2 * Math.Sqrt(p * (1 - p) / seeds);
            Row P(string group, string name, double rate, Func<ulong, FiberCutScenarios.Outcome> fly) =>
                new Row { Group = group, Name = name, Rate = rate, Low = rate - Spread(rate), High = rate + Spread(rate), Fly = fly };
            Row B(string group, string name, double low, double high, Func<ulong, FiberCutScenarios.Outcome> fly) =>
                new Row { Group = group, Name = name, Rate = double.NaN, Low = low, High = high, Fly = fly };
            var rows = new List<Row>
            {
                P("snap", $"sharp pitch, {Sharp:0}° in flight", 0.75, s => FiberCutScenarios.Snap(spec, WindLevel.Calm, s, 0, Sharp)),
                P("snap", $"sharp pitch, {Sharp:0}° in flight, full throttle through it", 0.80, s => FiberCutScenarios.Snap(spec, WindLevel.Calm, s, 0, Sharp, null, 1)),
                B("snap", $"sharp roll alone, {Sharp:0}° in flight (less than pitch)", 0, 0.75 - Spread(0.75), s => FiberCutScenarios.Snap(spec, WindLevel.Calm, s, 1, Sharp)),
                B("snap", $"sharp roll with yaw the same way, {Sharp:0}° in flight (at least pitch's)", 0.75 - Spread(0.75), 1, s => FiberCutScenarios.Snap(spec, WindLevel.Calm, s, 1, Sharp, null, double.NaN, true)),
                P("flip", "pitch flip, 360°", 1, s => FiberCutScenarios.Snap(spec, WindLevel.Calm, s, 0, Flip)),
                P("flip", "roll flip, 360°", 1, s => FiberCutScenarios.Snap(spec, WindLevel.Calm, s, 1, Flip)),
                P("brake", "top speed to a target, a proper brake", 0.25, s => FiberCutScenarios.Brake(spec, WindLevel.Calm, s)),
                P("brake", "a proper brake from a higher speed, 65 km/h", 0.07, s => FiberCutScenarios.Brake(spec, WindLevel.Calm, s, null, 18)),
                B("brake", "a mild approach, a brake from 43 km/h", 0, Rare, s => FiberCutScenarios.Brake(spec, WindLevel.Calm, s, null, 12)),
                P("liftoff", "very sharp liftoff (to 80 % throttle in 0.1 s)", 0.87, s => FiberCutScenarios.Liftoff(spec, true, s, null, WindLevel.Calm, 0.8)),
                P("liftoff", "very sharp liftoff at max throttle", 0.95, s => FiberCutScenarios.Liftoff(spec, true, s)),
                P("liftoff", "very sharp liftoff with the nose pulled up 20° (+10 points)", 0.97, s => FiberCutScenarios.Liftoff(spec, true, s, null, WindLevel.Calm, 0.8, -20)),
                P("liftoff", "the safe launch: max throttle with the nose pushed down 20°", 0.05, s => FiberCutScenarios.Liftoff(spec, true, s, null, WindLevel.Calm, 1, 20)),
                P("liftoff", "backwards too much at liftoff, the throttle not yet high", 0.40, s => FiberCutScenarios.Backward(spec, s, null, 3, 0.4)),
                B("normal", "normal liftoff, Calm", 0, Rare, s => FiberCutScenarios.Liftoff(spec, false, s)),
                B("normal", "normal liftoff, Windy", 0, Rare, s => FiberCutScenarios.Liftoff(spec, false, s, null, WindLevel.Windy)),
                B("normal", "hover at 3 m, Calm, 30 s", 0, Rare, s => FiberCutScenarios.Hover(spec, WindLevel.Calm, s)),
                B("normal", "orbit of 20 m at 54 km/h", 0, Rare, s => FiberCutScenarios.Turn(spec, WindLevel.Calm, s, 15, 43, 360, null, 21).O),
                B("normal", "gentle 180° turn of 15 m at 54 km/h", 0, Rare, s => FiberCutScenarios.Turn(spec, WindLevel.Calm, s, 15, 57, 180).O),
                B("normal", "cruise at 100 km/h, Windy", 0, Rare, s => FiberCutScenarios.Turn(spec, WindLevel.Windy, s, 27.8, 0, heightFirst: true).O),
                B("cause", "tree snag", 1.0 / seeds, 1, s => FiberCutScenarios.TreeSnag(spec, WindLevel.Calm, s)),
            };
            Console.WriteLine($"--cuts: the pilot's cut table (p7d-tether 1.11i), {rows.Count} rows of {seeds} seeds on the session's drone; " + Power.Describe());
            FiberCutScenarios.SessionDrone = true;
            FiberCutScenarios.StopAtBreak = true;
            try
            {
                foreach (Row r in rows) r.O = new FiberCutScenarios.Outcome[seeds];
                Parallel.For(0, rows.Count * seeds, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2) },
                    i => rows[i / seeds].O[i % seeds] = rows[i / seeds].Fly((ulong)(i % seeds + 1)));
            }
            finally
            {
                FiberCutScenarios.SessionDrone = false;
                FiberCutScenarios.StopAtBreak = false;
            }

            bool ok = true;
            foreach (Row r in rows)
                for (int s = 0; s < seeds; s++)
                    if (r.O[s].Broken) Console.WriteLine($"  {r.Name} seed {s + 1}: {r.O[s]}");
            Console.WriteLine($"--cuts: each row's rate against the pilot's (two binomial standard deviations at {seeds} seeds)");
            double Of(Row r) => (double)r.O.Count(o => o.Broken) / seeds;
            foreach (Row r in rows)
            {
                double rate = Of(r);
                bool met = rate >= Math.Max(0, r.Low) - 1e-9 && rate <= Math.Min(1, r.High) + 1e-9;
                ok &= met;
                string how = string.Join(", ", r.O.Where(o => o.Broken).GroupBy(o => $"{o.Mechanism} at {o.Place}").Select(g => $"{g.Key} {g.Count()}"));
                string target = double.IsNaN(r.Rate) ? "" : $"the pilot's {100 * r.Rate:0} %, ";
                Console.WriteLine($"  {(met ? "PASS" : "FAIL")} [{r.Group}] {r.Name}: {r.O.Count(o => o.Broken)} of {seeds} ({100 * rate:0} %) against {target}{100 * Math.Max(0, r.Low):0}–{100 * Math.Min(1, r.High):0} %{(how.Length > 0 ? ": " + how : "")}"
                    + $"; the hole's edge's most σ: mean {r.O.Average(o => o.EdgeMost) / 1e9:0.000} GPa, most {r.O.Max(o => o.EdgeMost) / 1e9:0.000} (θ up to {r.O.Max(o => o.EdgeTurn) * 180 / Math.PI:0}°, T up to {r.O.Max(o => o.EdgeTension):0.000} N); the exit's most tension {r.O.Average(o => o.ExitMost):0.000} N mean, {r.O.Max(o => o.ExitMost):0.000} most");
            }
            List<Row> normal = rows.Where(r => r.Group == "normal").ToList();
            int normalCuts = normal.Sum(r => r.O.Count(o => o.Broken)), flights = normal.Count * seeds;
            bool sometimes = normalCuts >= 1 && normalCuts <= Rare * flights;
            ok &= sometimes;
            Console.WriteLine($"  {(sometimes ? "PASS" : "FAIL")} [normal] the normal set: {normalCuts} of {flights} flights cut, against at least 1 and at most {Rare * flights:0} (\"sometimes, not too often\")");
            Console.WriteLine($"--cuts: {(ok ? "PASS" : "FAIL")}");
            return ok;
        }
    }
}
