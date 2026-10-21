using System;
using System.Diagnostics;
using System.IO;
using Fpv450.Sim.Flight;

namespace Fpv450.FlightBench
{
    // The mass build (task 1.3) on the repository's drone.json: the rebuild that the coil's payout will run inside a
    // step (7d), timed over a minute of 1 kHz payout, with its steady-state allocation.
    internal static class MassRebuildCase
    {
        public const string Name = "mass-rebuild";
        const string DroneJson = "assets-src/drone/drone.json";

        public static bool Bench()
        {
            const int warmup = 2000, steps = 60000;
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(FindRepoFile(DroneJson)));
            MassModel model = spec.BuildMass(spec.Nominal);
            int coil = spec.Coil.Part;
            double paid = spec.Nominal.CoilPaidOut;
            const double perStep = 0.05; // m of line per 1 ms step: 50 m/s, above the class's 80 km/h cruise
            var times = new long[steps];
            for (int k = 0; k < warmup; k++) model.SetPartMass(coil, spec.CoilMass(paid));

            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < steps; k++)
            {
                double mass = spec.CoilMass(paid + k * perStep);
                long t0 = Stopwatch.GetTimestamp();
                model.SetPartMass(coil, mass);
                times[k] = Stopwatch.GetTimestamp() - t0;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;

            Array.Sort(times);
            double us = 1e6 / Stopwatch.Frequency;
            double p50 = times[steps / 2] * us, p99 = times[steps * 99 / 100] * us;
            bool ok = allocated == 0;
            Console.WriteLine($"{Name}: {spec.Parts.Length} parts, {steps} rebuilds, p50 {p50:F2} µs, p99 {p99:F2} µs, "
                + $"steady-state allocation {allocated} B: {(ok ? "PASS" : "FAIL")} (0 B)");
            return ok;
        }

        // The file, from the working directory or the tool's own folder upwards.
        static string FindRepoFile(string relative)
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                for (string dir = start; dir != null; dir = Path.GetDirectoryName(dir))
                    if (File.Exists(Path.Combine(dir, relative)))
                        return Path.Combine(dir, relative);
            throw new FileNotFoundException(relative + " was not found above the working directory");
        }
    }
}
