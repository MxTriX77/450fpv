using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tests;
using Fpv450.Sim.World;

namespace Fpv450.FlightBench
{
    // A flight log's steps timed headless (w3-farmland-ground 2.1, PLT-04): the log's own flight replayed on this core, every
    // step record compared with the log's bit for bit (so every time is the pilot's flight, not a re-flight), and a window of it
    // timed twice. The first pass times the step alone (no timer inside it); the second wraps the world in TimedQuery for the
    // world queries' time in each step (its clock reads cost tens of ns a call, so its step is not the step). The window's
    // p50, p99 and maximum of both, and what the step allocated in it, are printed against PLT-04's 300 µs and 60 µs lines.
    // A log of an older QueryVersion is replayed with a note: a query answer that changed would show as a diverging record.
    internal static class ReplayStepCase
    {
        public static bool Run(string path, double fromS, double toS)
        {
            Console.WriteLine($"flightbench: the timed replay of {Path.GetFileName(path)}, flight seconds {fromS:F1} to {(toS < double.MaxValue ? toS.ToString("F1") : "the end")}; " + Power.Describe());
            if (!Pass(path, fromS, toS, false, out long[] steps, out int n, out long allocated, out string note)) return false;
            if (!Pass(path, fromS, toS, true, out long[] queries, out int nq, out _, out _)) return false;
            if (n == 0)
            {
                Console.WriteLine("  the window holds no step");
                return false;
            }
            double us = 1e6 / Stopwatch.Frequency;
            Array.Sort(steps, 0, n);
            Array.Sort(queries, 0, nq);
            double p99 = steps[n * 99 / 100] * us, q99 = queries[nq * 99 / 100] * us;
            bool ok = p99 <= 300 && q99 < 60 && allocated == 0;
            Console.WriteLine($"  {note}");
            Console.WriteLine($"  the window's {n} steps: the step p50 {steps[n / 2] * us:F1} µs, p99 {p99:F1} µs, max {steps[n - 1] * us:F1} µs; "
                + $"its world queries p50 {queries[nq / 2] * us:F1} µs, p99 {q99:F1} µs, max {queries[nq - 1] * us:F1} µs; allocated {allocated} B: "
                + (ok ? "PASS" : "FAIL") + " (PLT-04: the step's p99 ≤ 300 µs, its world queries < 60 µs at p99, 0 B)");
            return ok;
        }

        // One pass over the log: every step replayed and checked; the window's step times (or, timed, its queries' times).
        static bool Pass(string path, double fromS, double toS, bool timed, out long[] times, out int count, out long allocated, out string note)
        {
            times = Array.Empty<long>();
            count = 0;
            allocated = 0;
            note = null;
            using FlightLogReader log = FlightLogReader.Open(path);
            LogHeader h = log.Header;
            if (h.ModelVersion != LogFormat.ModelVersion)
            {
                Console.WriteLine($"  refused: the log is of model {h.ModelVersion}, this core is model {LogFormat.ModelVersion}");
                return false;
            }
            IWorldQuery map = h.WorldId == "floor" ? FlightWorld.Floor().Query : MapWorld.Load(RangeScenarios.Root, h.WorldId);
            TimedQuery clock = timed ? new TimedQuery(map) : null;
            var world = new FlightWorld(timed ? clock : map);
            if (world.ContentHash != h.WorldHash)
            {
                Console.WriteLine($"  refused: the world '{h.WorldId}' differs from the log's");
                return false;
            }
            var problems = new List<string>();
            FlightSetup setup = h.ToSetup(world, problems);
            if (problems.Count > 0)
            {
                Console.WriteLine("  refused: " + problems[0]);
                return false;
            }
            note = $"seed 0x{h.Seed:X16}, model {h.ModelVersion}, world '{h.WorldId}'"
                + (h.QueryVersion != world.QueryVersion ? $", logged at QueryVersion {h.QueryVersion} and replayed at {world.QueryVersion}" : "")
                + $"; recorded on {h.Runtime}, replayed on {LogHeader.CurrentRuntime()}";
            var core = new FlightCore(setup);
            using Fpv450.Sim.Wind.WindField air = core.Wind;
            long first = (long)Math.Round(fromS * 1000), last = toS < double.MaxValue ? (long)Math.Round(toS * 1000) : long.MaxValue;
            times = new long[(int)Math.Max(0, Math.Min(last, 1000000) - first + 1)];
            var replayed = new byte[LogFormat.StepSize];
            long records = 0;
            while (log.Next())
            {
                if (log.Kind != LogFormat.StepBlock) continue;
                LogFormat.DecodeStick(log.Payload, out StickSample stick);
                long a = GC.GetAllocatedBytesForCurrentThread(), q = timed ? clock.Ticks : 0, t0 = Stopwatch.GetTimestamp();
                core.Advance(stick);
                long dt = Stopwatch.GetTimestamp() - t0, b = GC.GetAllocatedBytesForCurrentThread();
                long step = core.State.Step;
                LogFormat.EncodeStep(replayed, core.State, stick, core.Trace);
                if (LogFormat.StepOf(log.Payload) != step || !log.Payload.SequenceEqual(replayed))
                {
                    Console.WriteLine($"  DIVERGED at step {step}: the replayed record differs from the log's");
                    return false;
                }
                records++;
                if (step < first || step > last || count == times.Length) continue;
                times[count++] = timed ? clock.Ticks - q : dt;
                allocated += b - a;
            }
            note += $"; {records} step records equal the log's bit for bit";
            return true;
        }
    }
}
