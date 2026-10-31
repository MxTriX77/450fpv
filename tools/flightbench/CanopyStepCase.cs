using System;
using System.Diagnostics;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;

namespace Fpv450.FlightBench
{
    // The step inside a wooded strip (p7c-wind 1.5): the wind harness's Severe flight, nominal pilot, seed 1, released at
    // its belt height and flown through its strip (CanopyFlow: 64 more modes at every sample inside it). The bench times
    // the step inside the strip and outside it; the pilot's own work is outside the timed calls.
    internal static class CanopyStepCase
    {
        public const string Name = "canopy-step";

        public static bool Bench()
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            double low = WindHarness.BeltHeightOf(2, 5);
            FlightSetup setup = WindHarness.Setup(spec, WindLevel.Severe, 1, low, belt: true, drag: true, out double courseDeg, out _);
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new HarnessPilot(core.Controller) { Course = courseDeg * 0.017453292519943295, Height = low };
            const int warmup = 2000, steps = 25000; // through the strip by about 16 s
            var inside = new long[steps];
            var outside = new long[steps];
            int ni = 0, no = 0, frame = 0;
            long release = core.State.Step, allocated = 0;
            for (int k = 0; k < warmup + steps; k++)
            {
                if (k * HarnessPilot.CameraHz >= frame * 1000.0)
                {
                    pilot.See(HarnessPilot.Take(core.State, k, pilot.Course));
                    frame++;
                }
                StickSample stick = pilot.Sticks(k, (release + k) * FlightCore.StepUs);
                bool inStrip = core.Wind.Canopy.Weight(core.State.Position) > 0;
                long a = GC.GetAllocatedBytesForCurrentThread(), t0 = Stopwatch.GetTimestamp();
                core.Advance(stick);
                long t = Stopwatch.GetTimestamp() - t0;
                if (k < warmup) continue;
                allocated += GC.GetAllocatedBytesForCurrentThread() - a;
                if (inStrip) inside[ni++] = t;
                else outside[no++] = t;
            }
            double us = 1e6 / Stopwatch.Frequency;
            double P(long[] x, int n, int pc)
            {
                Array.Sort(x, 0, n);
                return n == 0 ? double.NaN : x[Math.Min(n - 1, n * pc / 100)] * us;
            }
            double i50 = P(inside, ni, 50), i99 = P(inside, ni, 99), o50 = P(outside, no, 50), o99 = P(outside, no, 99);
            bool ok = ni > 3000 && i99 <= 300 && o99 <= 300 && allocated == 0 && core.State.Armed;
            Console.WriteLine($"{Name}: the harness's Severe flight at {low:F1} m, {ni} steps inside the strip p50 {i50:F2} µs, p99 {i99:F2} µs; "
                + $"{no} outside p50 {o50:F2} µs, p99 {o99:F2} µs; allocation {allocated} B: {(ok ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B)");
            return ok;
        }
    }
}
