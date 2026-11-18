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
    // The pilot's cut targets (p7d-tether 1.11i; the pilot, 2026-11-18, D-104): each row's rate of cuts over seeds 1–N against
    // its band, red outside it in either direction. The bands are the orchestrator's reading of the pilot's words: a sharp pitch
    // or roll parts the line in 60–90 % of tries ("they don't forgive 75 %"), a flip every time ("never forgive flips"), normal
    // flying now and then and at most about 1 flight in 10 ("sometimes random, not too often": at least 1 across the normal set),
    // the causes on record on some seeds. Every later fiber change must pass it. The cuts are the line's own mechanics.
    internal static class CutsCase
    {
        sealed class Row
        {
            public string Name, Group;
            public double Low, High; // the band of the cut rate, fractions of the seeds
            public Func<ulong, FiberCutScenarios.Outcome> Fly;
            public FiberCutScenarios.Outcome[] O;
        }

        public static bool Run(int seeds)
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            // A sharp snap's turn: the pilot's own (their logs: fly snaps, 1.11i), at their 225 °/s full-stick rate.
            const double Sharp = 120, Flip = 360;
            Row R(string group, string name, double low, double high, Func<ulong, FiberCutScenarios.Outcome> fly) =>
                new Row { Group = group, Name = name, Low = low, High = high, Fly = fly };
            const double Rare = 0.1; // "not too often": at most about 1 flight in 10 (the orchestrator's reading)
            var rows = new List<Row>
            {
                R("snap", $"sharp pitch, {Sharp:0}° at full stick, from 10 m/s", 0.6, 0.9, s => FiberCutScenarios.Snap(spec, WindLevel.Calm, s, 0, Sharp)),
                R("snap", $"sharp roll, {Sharp:0}° at full stick, from 10 m/s", 0.6, 0.9, s => FiberCutScenarios.Snap(spec, WindLevel.Calm, s, 1, Sharp)),
                R("flip", "pitch flip, 360° at full stick", 1, 1, s => FiberCutScenarios.Snap(spec, WindLevel.Calm, s, 0, Flip)),
                R("flip", "roll flip, 360° at full stick", 1, 1, s => FiberCutScenarios.Snap(spec, WindLevel.Calm, s, 1, Flip)),
                R("normal", "normal liftoff, Calm", 0, Rare, s => FiberCutScenarios.Liftoff(spec, false, s)),
                R("normal", "normal liftoff, Windy", 0, Rare, s => FiberCutScenarios.Liftoff(spec, false, s, null, WindLevel.Windy)),
                R("normal", "hover at 3 m, Calm, 30 s", 0, Rare, s => FiberCutScenarios.Hover(spec, WindLevel.Calm, s)),
                R("normal", "hover at 3 m, Windy, 30 s", 0, Rare, s => FiberCutScenarios.Hover(spec, WindLevel.Windy, s)),
                R("normal", "orbit of 20 m at 54 km/h", 0, Rare, s => FiberCutScenarios.Turn(spec, WindLevel.Calm, s, 15, 43, 360, null, 21).O),
                R("normal", "gentle 180° turn of 15 m at 54 km/h", 0, Rare, s => FiberCutScenarios.Turn(spec, WindLevel.Calm, s, 15, 57, 180).O),
                R("normal", "cruise at 100 km/h, Calm", 0, Rare, s => FiberCutScenarios.Turn(spec, WindLevel.Calm, s, 27.8, 0, heightFirst: true).O),
                R("normal", "cruise at 100 km/h, Windy", 0, Rare, s => FiberCutScenarios.Turn(spec, WindLevel.Windy, s, 27.8, 0, heightFirst: true).O),
                R("cause", "overly sharp reverse from 54 km/h, Calm", 1.0 / 20, 19.0 / 20, s => FiberCutScenarios.SharpReverse(spec, WindLevel.Calm, s)),
                R("cause", "overly sharp reverse from 54 km/h, Windy", 1.0 / 20, 19.0 / 20, s => FiberCutScenarios.SharpReverse(spec, WindLevel.Windy, s)),
                R("cause", "tree snag", 1.0 / 20, 1, s => FiberCutScenarios.TreeSnag(spec, WindLevel.Calm, s)),
                R("cause", "very sharp liftoff", 1.0 / 20, 1, s => FiberCutScenarios.Liftoff(spec, true, s)),
                R("cause", "backward momentum right after liftoff", 1.0 / 20, 1, s => FiberCutScenarios.Backward(spec, s)),
            };
            Console.WriteLine($"--cuts: the pilot's cut targets (p7d-tether 1.11i), {rows.Count} rows of {seeds} seeds on the session's drone; " + Power.Describe());
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
            Console.WriteLine("--cuts: each row's rate against its band");
            foreach (Row r in rows)
            {
                int c = r.O.Count(o => o.Broken);
                double rate = (double)c / seeds;
                bool met = rate >= r.Low - 1e-9 && rate <= r.High + 1e-9;
                ok &= met;
                string how = string.Join(", ", r.O.Where(o => o.Broken).GroupBy(o => $"{o.Mechanism} at {o.Place}").Select(g => $"{g.Key} {g.Count()}"));
                Console.WriteLine($"  {(met ? "PASS" : "FAIL")} [{r.Group}] {r.Name}: {c} of {seeds} ({100 * rate:0} %) against {100 * r.Low:0}–{100 * r.High:0} %{(how.Length > 0 ? ": " + how : "")}");
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
