using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Fpv450.Sim.Tests;
using Fpv450.Sim.World;

namespace Fpv450.WorldBench
{
    // The wake grid (p7c-wind 1.3) over its synthetic layout (WakeGridFixture): the golden cases, the scenario numbers
    // and the worker under a 1 kHz loop.
    internal static class WakeBench
    {
        private static readonly double[] GoldenDirections = { 0, 10, 37, 90, 215.5 };

        private static ulong Mix(ulong hash, double v)
        {
            ulong bits = (ulong)BitConverter.DoubleToInt64Bits(v);
            for (int b = 0; b < 8; b++) hash = (hash ^ (byte)(bits >> 8 * b)) * 0x100000001B3UL;
            return hash;
        }

        private static string Hex(ulong v) => v.ToString("x16", CultureInfo.InvariantCulture);

        /// <summary>The golden cases: each direction's grid hash (built twice, and rebuilt in place, which must agree) and a
        /// hash of 10 000 interpolated samples in the belt's and the house's wakes.</summary>
        public static List<(string Name, string Value, bool Agree)> Cases()
        {
            WakeObstacles obstacles = WakeGridFixture.Obstacles();
            var cases = new List<(string, string, bool)> { ("wake.version", WakeGrid.Version.ToString(CultureInfo.InvariantCulture), true) };
            WakeGrid spare = WakeGrid.Build(obstacles, 180);
            foreach (double deg in GoldenDirections)
            {
                WakeGrid a = WakeGrid.Build(obstacles, deg), b = WakeGrid.Build(obstacles, deg);
                spare.Rebuild(deg);
                ulong h = a.Hash();
                cases.Add(("wake.grid." + deg.ToString("R", CultureInfo.InvariantCulture), Hex(h), h == b.Hash() && h == spare.Hash()));

                ulong s = Fpv450.Sim.World.WorldHash.Offset;
                ulong state = 0x9E3779B97F4A7C15UL * (ulong)(deg * 10 + 1);
                for (int i = 0; i < 10000; i++)
                {
                    double x = -160 + 320 * Next(ref state), z = -120 + 340 * Next(ref state), y = 0.2 + 40 * Next(ref state);
                    a.Sample(x, z, y, out WakeSample w);
                    s = Mix(Mix(Mix(s, w.Speed), w.Turbulence), w.ObstacleHeight);
                }
                cases.Add(("wake.samples." + deg.ToString("R", CultureInfo.InvariantCulture), Hex(s), true));
            }
            return cases;
        }

        private static double Next(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return ((z ^ (z >> 31)) >> 11) * (1.0 / 9007199254740992.0);
        }

        // ---- The scenario numbers ----------------------------------------------------------------------------------

        private static int Column(double x) => (int)((x + 300) / 2);

        private static double CentreZ(int j) => -300 + 2 * j + 1;

        /// <summary>Perera (1981)'s R, written out again here with System.Math (the reference for the grid's DetMath one).</summary>
        public static double Perera(double x, double z, double beta, double h)
        {
            double k = 2 * 0.4 * 0.4 / Math.Log(h / 0.03);
            double eta = z * Math.Pow(k * x, -1 / 2.14);
            if (eta < 0.99667) eta = 0.99667;
            return 9.75 * (1 - beta) * eta * Math.Exp(-0.67 * Math.Pow(eta, 1.5)) / x;
        }

        public static void Scenarios()
        {
            WakeObstacles obstacles = WakeGridFixture.Obstacles();
            WakeGrid grid = WakeGrid.Build(obstacles, 0);
            Console.WriteLine("  wind from 0 deg (north); the belt's leeward edge at z = -3.1 m, H = 8.4 m");

            // The belt, at x = -59 (far from its ends and the gap).
            int i = Column(-60);
            Console.WriteLine("  belt at x = -59 m:   X=x/H   beta    phi   u(0.25H) u(0.5H) Perera(0.5H) u(1H)  u(1.2H) | T(0.5H) T(1H) T(2H) T(3H)");
            double sum = 0;
            int n = 0;
            for (int j = 147; j >= 0; j--)
            {
                grid.Describe(i, j, out double x, out double h, out _, out double beta, out double phi, out _, out _);
                if (!(h > 0)) continue;
                bool print = false;
                foreach (double target in new[] { 0.0, 0.5, 1, 2, 3, 5, 7.5, 10, 12.5, 15, 20, 25, 30 })
                    print |= Math.Abs(x - target) <= 0.12;
                if (x >= 5 && x <= 15)
                {
                    grid.SampleCell(i, j, 0.5 * h, out WakeSample m);
                    sum += m.Turbulence;
                    n++;
                }
                if (!print) continue;
                grid.SampleCell(i, j, 0.25 * h, out WakeSample a);
                grid.SampleCell(i, j, 0.5 * h, out WakeSample b);
                grid.SampleCell(i, j, 1.0 * h, out WakeSample c);
                grid.SampleCell(i, j, 1.2 * h, out WakeSample d);
                grid.SampleCell(i, j, 2.0 * h, out WakeSample e);
                grid.SampleCell(i, j, 3.0 * h, out WakeSample f);
                string perera = x >= 5 ? (1 - phi * Perera(x, 0.5, beta, h)).ToString("F3", CultureInfo.InvariantCulture) : "  -  ";
                Console.WriteLine(FormattableString.Invariant(
                    $"    z {CentreZ(j),7:F1}: {x,6:F2} {beta,6:F3} {phi,6:F3}  {a.Speed,7:F3} {b.Speed,7:F3}  {perera,8}    {c.Speed,6:F3} {d.Speed,6:F3} | {b.Turbulence,6:F3} {c.Turbulence,5:F3} {e.Turbulence,5:F3} {f.Turbulence,5:F3}"));
            }
            Console.WriteLine(FormattableString.Invariant($"  belt: mean turbulence ratio over 5-15 H at 0.5 H: {sum / n:F3} ({n} cells; literature about 2x, K8.2)"));
            var tops = new List<string>();
            foreach (double target in new[] { 5.0, 10, 15, 20 })
            {
                int best = -1;
                double err = double.MaxValue;
                for (int j = 147; j >= 0; j--)
                {
                    grid.Describe(i, j, out double x, out double h, out _, out _, out _, out _, out _);
                    if (h > 0 && Math.Abs(x - target) < err) { err = Math.Abs(x - target); best = j; }
                }
                grid.SampleCell(i, best, 0.5 * 8.4, out WakeSample low);
                double top = 0;
                for (double zh = 0.5; zh < 6; zh += 0.01)
                {
                    grid.SampleCell(i, best, zh * 8.4, out WakeSample s);
                    if (s.Turbulence - 1 < 0.1 * (low.Turbulence - 1)) { top = zh; break; }
                }
                tops.Add(FormattableString.Invariant($"{top:F2} H at {target} H"));
            }
            Console.WriteLine("  belt: the wake's top (the boost down to a tenth of its value at 0.5 H): " + string.Join(", ", tops) + " (K8.2: 2-3 H)");
            int lowest = 0;
            for (int j = 0; j < 150; j++)
                for (int a = Column(-150); a <= Column(150); a++)
                    foreach (double zh in new[] { 0.1, 0.25, 0.5, 1.0 })
                    {
                        grid.SampleCell(a, j, zh * 8.4, out WakeSample s);
                        if (s.Speed < 0) lowest++;
                    }
            Console.WriteLine($"  belt: cells with reverse flow anywhere behind it: {lowest} (porous, beta > 0.3: none)");

            // The house, at x = -99.
            i = Column(-100);
            double xr = WakeGrid.Reattachment(5, 10, 6);
            Console.WriteLine(FormattableString.Invariant($"  solid house at x = -99 m (H 5, W 10, L 6): Fackrell's reattachment X_R = {xr:F2} H"));
            Console.WriteLine("    X=x/H    u(0.1H)  u(0.25H)  u(0.5H)  u(0.9H)  u(1.2H) | T(0.5H)");
            double lastReverse = 0, firstReverse = double.PositiveInfinity, minSpeed = 1;
            for (int j = Column(WakeGridFixture.HouseLeeZ) ; j >= Column(WakeGridFixture.HouseLeeZ - 100); j--)
            {
                grid.Describe(i, j, out double x, out double h, out _, out _, out _, out _, out _);
                if (!(h > 0) || CentreZ(j) > WakeGridFixture.HouseLeeZ) continue;
                double[] zs = { 0.1, 0.25, 0.5, 0.9, 1.2 };
                var u = new double[zs.Length];
                double t = 0;
                for (int q = 0; q < zs.Length; q++)
                {
                    grid.SampleCell(i, j, zs[q] * h, out WakeSample s);
                    u[q] = s.Speed;
                    if (q == 2) t = s.Turbulence;
                    if (s.Speed < 0) { lastReverse = Math.Max(lastReverse, x); firstReverse = Math.Min(firstReverse, x); }
                    minSpeed = Math.Min(minSpeed, s.Speed);
                }
                if (x <= 4.2 || Math.Abs(x - 6) < 0.25 || Math.Abs(x - 10) < 0.25 || Math.Abs(x - 20) < 0.25)
                    Console.WriteLine(FormattableString.Invariant($"    {x,6:F2}  {u[0],7:F3}  {u[1],7:F3}  {u[2],7:F3}  {u[3],7:F3}  {u[4],7:F3} | {t,6:F3}"));
            }
            Console.WriteLine(FormattableString.Invariant($"  house: reverse flow from X = {firstReverse:F2} to {lastReverse:F2} H, strongest u/U = {minSpeed:F3}"));

            // The gap, at x = 57 (its middle is 56.6), against the belt at x = -59.
            int g = Column(WakeGridFixture.GapMiddle), b0 = Column(-60);
            Console.WriteLine("  the gap at x = 57 m against the belt at x = -59 m, at 0.5 H (4.2 m):");
            Console.WriteLine("    z m      x/H   gap u   gap T  jet   | belt u  belt T");
            for (int j = 147; j >= 60; j--)
            {
                double z = CentreZ(j), x = (WakeGridFixture.BeltLeeZ - z) / 8.4;
                if (!(Math.Abs(x - Math.Round(x)) < 0.12 && (x <= 6 || Math.Abs(x % 5) < 0.12))) continue;
                grid.SampleCell(g, j, 4.2, out WakeSample s);
                grid.SampleCell(b0, j, 4.2, out WakeSample r);
                grid.Describe(g, j, out _, out _, out _, out _, out _, out double jet, out _);
                Console.WriteLine(FormattableString.Invariant($"    {z,6:F1}  {x,6:F2}  {s.Speed,6:F3}  {s.Turbulence,6:F3}  {jet,5:F3} | {r.Speed,6:F3}  {r.Turbulence,6:F3}"));
            }
        }

        // ---- The worker under a 1 kHz loop -------------------------------------------------------------------------

        private enum Mode { Control, Worker, OnTheStep }

        private sealed class Run
        {
            public long[] Work, Late;
            public long Allocated, RequestStep = -1, TakeoverStep = -1;
            public int Gc0, Gc1, Gc2, Stalls, Builds;
            public double BuildMs;
            public ulong Hash;
            public double FromDeg;
        }

        private const int Steps = 10000, Points = 38, ChangeStep = 1000;

        /// <summary>
        /// A 1 kHz loop on its own thread for 10 s: each step calls the worker's Step and samples 38 points (4 rotors, the
        /// body, the coil, 32 tether nodes: K§10.5's wind points) flying at 13 m/s across the belt's wake, each step paced
        /// to its 1 ms deadline by spinning. Worker: the wind turns from 0 to 10 degrees at 1 s. Control: it never turns.
        /// OnTheStep (the known-wrong design): the step rebuilds the grid itself when the wind turns.
        /// </summary>
        private static Run Fly(WakeObstacles obstacles, Mode mode)
        {
            var run = new Run { Work = new long[Steps], Late = new long[Steps] };
            using var worker = new WakeGridWorker(obstacles, 0);
            WakeGrid own = WakeGrid.Build(obstacles, 0);
            double checksum = 0;
            var loop = new Thread(() =>
            {
                double tick = Stopwatch.Frequency / 1000.0;
                for (int w = 0; w < 2000; w++) // warm the JIT before the clock starts
                {
                    worker.Sample(-60 + w * 0.01, -20, 20, out WakeSample s);
                    own.Sample(-60 + w * 0.01, -20, 20, out s);
                    checksum += s.Speed;
                }
                int c0 = GC.CollectionCount(0), c1 = GC.CollectionCount(1), c2 = GC.CollectionCount(2);
                long a0 = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                for (int s = 0; s < Steps; s++)
                {
                    long deadline = start + (long)(s * tick);
                    long now;
                    while ((now = Stopwatch.GetTimestamp()) < deadline) { }
                    run.Late[s] = now - deadline;
                    double deg = mode == Mode.Control || s < ChangeStep ? 0 : 10;
                    double t = s * 0.001, px = -100 + 13 * t, pz = -40;
                    if (mode == Mode.OnTheStep)
                    {
                        if (own.FromDeg != deg) own.Rebuild(deg);
                        for (int p = 0; p < Points; p++)
                        {
                            own.Sample(px + 0.17 * p, pz - 0.31 * p, 6 + 0.4 * p, out WakeSample w);
                            checksum += w.Speed + w.Turbulence;
                        }
                    }
                    else
                    {
                        worker.Step(s, deg);
                        if (run.RequestStep < 0 && worker.Pending) run.RequestStep = s;
                        if (run.TakeoverStep < 0 && worker.Fading != null) run.TakeoverStep = s;
                        for (int p = 0; p < Points; p++)
                        {
                            worker.Sample(px + 0.17 * p, pz - 0.31 * p, 6 + 0.4 * p, out WakeSample w);
                            checksum += w.Speed + w.Turbulence;
                        }
                    }
                    run.Work[s] = Stopwatch.GetTimestamp() - now;
                }
                run.Allocated = GC.GetAllocatedBytesForCurrentThread() - a0;
                run.Gc0 = GC.CollectionCount(0) - c0;
                run.Gc1 = GC.CollectionCount(1) - c1;
                run.Gc2 = GC.CollectionCount(2) - c2;
            }) { Priority = ThreadPriority.Highest, Name = "1 kHz loop" };
            loop.Start();
            loop.Join();
            run.Stalls = worker.Stalls;
            run.Builds = worker.Builds;
            run.BuildMs = worker.LastBuildSeconds * 1e3;
            run.Hash = mode == Mode.OnTheStep ? own.Hash() : worker.Active.Hash();
            run.FromDeg = mode == Mode.OnTheStep ? own.FromDeg : worker.Active.FromDeg;
            GC.KeepAlive(checksum);
            return run;
        }

        private static void Print(string name, Run run, int from, int to)
        {
            double us = 1e6 / Stopwatch.Frequency;
            Stats(run.Work, from, to, out double med, out double p99, out double max);
            Stats(run.Late, from, to, out double lmed, out double lp99, out double lmax);
            int missed = 0;
            var where = new List<string>();
            for (int s = from; s < to; s++)
                if (Missed(run, s))
                {
                    missed++;
                    if (where.Count < 8) where.Add(s.ToString(CultureInfo.InvariantCulture));
                }
            Console.WriteLine(FormattableString.Invariant(
                $"    {name,-34} steps {from,5}..{to,5}: work median {med * us,6:F2} p99 {p99 * us,7:F2} max {max * us,9:F2} us; start late max {lmax * us,9:F2} us; {missed} steps over 1 ms{(where.Count > 0 ? " (at " + string.Join(", ", where) + (missed > where.Count ? ", ..." : "") + ")" : "")}"));
        }

        public static int Loop(string power)
        {
            WakeObstacles obstacles = WakeGridFixture.Obstacles();
            var times = new List<double>();
            for (int r = 0; r < 3; r++)
            {
                long t0 = Stopwatch.GetTimestamp();
                WakeGrid.Build(obstacles, 10);
                times.Add((Stopwatch.GetTimestamp() - t0) * 1e3 / Stopwatch.Frequency);
            }
            times.Sort();
            Console.WriteLine(FormattableString.Invariant($"  build on the calling thread (300 x 300 cells, 10 deg): median {times[1]:F1} ms of 3 (min {times[0]:F1}, max {times[2]:F1})"));
            Console.WriteLine($"  1 kHz loop, {Steps} steps of Step + {Points} samples each ({power}):");

            Run control = Fly(obstacles, Mode.Control);
            Print("control (the wind never turns)", control, 0, Steps);
            Run worker = Fly(obstacles, Mode.Worker);
            Print("worker (0 to 10 deg at 1 s), all", worker, 0, Steps);
            Print("  from the request to the takeover", worker, (int)worker.RequestStep, (int)worker.TakeoverStep);
            Print("  during the fade (two grids)", worker, (int)worker.TakeoverStep, (int)Math.Min(Steps, worker.TakeoverStep + WakeGridWorker.FadeSteps));
            Console.WriteLine(FormattableString.Invariant(
                $"      worker: {worker.Builds} build(s), {worker.BuildMs:F1} ms on the worker; request at step {worker.RequestStep}, takeover at {worker.TakeoverStep} (+{WakeGridWorker.LatencySteps}); stalls {worker.Stalls}; flight thread {worker.Allocated} B allocated; GCs gen0 {worker.Gc0} gen1 {worker.Gc1} gen2 {worker.Gc2}"));
            Run wrong = Fly(obstacles, Mode.OnTheStep);
            Print("known-wrong: rebuilt on the step", wrong, 0, Steps);

            ulong direct = WakeGrid.Build(obstacles, 10).Hash();
            bool same = worker.Hash == direct && wrong.Hash == direct;
            Console.WriteLine($"    the worker's grid at {worker.FromDeg} deg {(same ? "equals" : "DIFFERS FROM")} a direct build: {Hex(worker.Hash)} ({Hex(direct)})");

            // A lone step over 1 ms is the machine's noise floor (the control shows it too); a hitch is a run of them,
            // as the known-wrong design's. The worker passes with no run longer than the control's longest.
            int windowMissed = Over(worker, (int)worker.RequestStep, (int)worker.TakeoverStep);
            int wrongMissed = Over(wrong, 0, Steps);
            Console.WriteLine($"    longest run of consecutive steps over 1 ms: control {Longest(control)}, worker {Longest(worker)}, known-wrong {Longest(wrong)}");
            bool ok = worker.Allocated == 0 && worker.Stalls == 0 && worker.Gc0 + worker.Gc1 + worker.Gc2 == 0 && same
                && worker.FromDeg == 10 && Longest(worker) <= Math.Max(2, Longest(control)) && Longest(wrong) > 50;
            Console.WriteLine($"    steps over 1 ms while the worker built: {windowMissed}; the known-wrong design's: {wrongMissed} (it must show the stall)");
            Console.WriteLine(ok ? "worldbench --wakes: PASS" : "worldbench --wakes: FAIL");
            return ok ? 0 : 1;
        }

        private static bool Missed(Run run, int s) => (run.Late[s] + run.Work[s]) * 1e6 / Stopwatch.Frequency >= 1000;

        private static int Over(Run run, int from, int to)
        {
            int n = 0;
            for (int s = from; s < to; s++) n += Missed(run, s) ? 1 : 0;
            return n;
        }

        private static int Longest(Run run)
        {
            int best = 0, current = 0;
            for (int s = 2; s < Steps; s++) // the loop's first two steps start it
            {
                current = Missed(run, s) ? current + 1 : 0;
                if (current > best) best = current;
            }
            return best;
        }

        private static void Stats(long[] v, int from, int to, out double median, out double p99, out double max)
        {
            if (to <= from) { median = p99 = max = 0; return; }
            var part = new long[to - from];
            Array.Copy(v, from, part, 0, part.Length);
            Array.Sort(part);
            median = part[part.Length / 2];
            p99 = part[Math.Min(part.Length - 1, part.Length * 99 / 100)];
            max = part[part.Length - 1];
        }
    }
}