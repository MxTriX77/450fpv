using System;
using System.Diagnostics;
using System.IO;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // The step with the log on (tasks 1.2, 1.4 and 1.5, design 10): the powered drone with its drag in the Windy wind,
    // flown by GoldenFlights' scripted pilot at a real 1 kHz for 30 s, each step's 1296 B record going through FlightRecorder's ring to a log file on disk while
    // its flush worker writes, as in a flight. It times the step with its record, and the record alone, and counts the
    // flight thread's allocations (the budget: p99 ≤ 300 µs and 0 B, PLT-04) and the ring's waits for the disk (0: the
    // step never waits on it).
    internal static class LoggedStepCase
    {
        public const string Name = "logged-step";

        public static bool Bench()
        {
            const int warmup = 2000, steps = 30000; // 30 s at 1 kHz: the 4 MiB ring turns over 6.5 times
            FlightSetup setup = GoldenFlights.PoweredSetup(PoweredFlightCase.Drone());
            var core = new FlightCore(setup);
            LogHeader header = LogHeader.For(setup, core);
            header.Build = "tools/flightbench";
            header.WorldId = "floor";
            // A name of its own, so benches run side by side (parallel agents' worktrees) never share the file.
            string path = Path.Combine(Path.GetTempPath(), "fpv450-flightbench-" + Guid.NewGuid().ToString("N") + LogStore.Extension);
            var recorder = new FlightRecorder(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16), header);

            var total = new long[steps];
            var record = new long[steps];
            long period = Stopwatch.Frequency / 1000;
            long next = Stopwatch.GetTimestamp();
            long allocated = 0, processAllocated = 0;
            for (int k = -warmup; k < steps; k++)
            {
                if (k == 0)
                {
                    allocated = GC.GetAllocatedBytesForCurrentThread();
                    processAllocated = GC.GetTotalAllocatedBytes(true);
                }
                StickSample stick = GoldenFlights.PoweredStick((k + warmup) % GoldenFlights.PoweredSteps);
                if (k + warmup >= GoldenFlights.PoweredSteps) stick.Arm = true; // the script's restart keeps the switch on
                while (Stopwatch.GetTimestamp() < next) { } // the 1 kHz pace, as the flight thread keeps it
                next += period;
                long t0 = Stopwatch.GetTimestamp();
                core.Advance(stick);
                long t1 = Stopwatch.GetTimestamp();
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
                long t2 = Stopwatch.GetTimestamp();
                if (k >= 0)
                {
                    total[k] = t2 - t0;
                    record[k] = t2 - t1;
                }
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            processAllocated = GC.GetTotalAllocatedBytes(true) - processAllocated;
            recorder.Close();
            long bytes = new FileInfo(path).Length;
            File.Delete(path);

            Array.Sort(total);
            Array.Sort(record);
            double us = 1e6 / Stopwatch.Frequency;
            double p50 = total[steps / 2] * us, p99 = total[steps * 99 / 100] * us, max = total[steps - 1] * us;
            double r50 = record[steps / 2] * us, r99 = record[steps * 99 / 100] * us, rmax = record[steps - 1] * us;
            LogEnd c = recorder.Counts;
            bool ok = p99 <= 300 && allocated == 0 && c.LogWaits == 0 && recorder.Fault == null && c.Steps == warmup + steps;
            Console.WriteLine($"{Name}: {steps} steps at 1 kHz into the log on disk ({bytes} bytes, {c.Steps} records, {c.LogWaits} ring waits): "
                + $"step with its record p50 {p50:F2} µs, p99 {p99:F2} µs, max {max:F1} µs; the record alone p50 {r50:F2} µs, p99 {r99:F2} µs, "
                + $"max {rmax:F1} µs; flight-thread allocation {allocated} B (the whole process, flush worker included: {processAllocated} B): "
                + $"{(ok ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B, 0 waits)");
            return ok;
        }
    }
}
