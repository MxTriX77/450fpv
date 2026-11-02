using System;
using System.Diagnostics;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;

namespace Fpv450.FlightBench
{
    // The cost of the wind (p7c-wind 1.8, design 8): Severe wind in Severe rain over the posadka, the whole airframe. The
    // posadka is the wake grid's synthetic belt (WakeGridFixture: a maple row with a gap and a solid house, D-046: the test
    // range's layout lives on its own branch). The drone holds a point 2 H downwind of the belt's middle at 0.75 H (its
    // wake's turbulent layer), from the second of seed 1's direction history where the bearing moves 10° or more within the
    // flight, so the worker rebuilds the grid at least twice. The flight is paced at 1 kHz of wall time, as the sim's own
    // thread is (unpaced, 3 s of latency would pass in about 70 ms and the worker could not keep up with a faster-than-real
    // flight). The bench times the step and checks that the flight thread never waits for the worker
    // (WakeGridWorker.Stalls); the pilot's own work and the pacing are outside the timed calls.
    internal static class PosadkaStepCase
    {
        public const string Name = "posadka-step";

        const ulong Seed = 1;
        const int Seconds = 60, Warmup = 2000;
        const double Turn = 10;

        public static bool Bench()
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            int start = FirstTurn(out double turned);
            WindSetup wind = WindSetup.Of(WindLevel.Severe);
            wind.StartSecond = start;
            wind.Wakes = new WakeLayout { OriginX = -300, OriginZ = -300, NX = 300, NZ = 300, Volumes = WakeGridFixture.Volumes() };
            FlightSetup setup = CrosswindScenario.Setup(spec, wind, Seed);
            setup.Rain = RainLevel.SevereRain;
            setup.Asymmetry = spec.BuildAsymmetry();
            setup.VortexRing = spec.BuildVortexRing();
            setup.GroundEffect = spec.BuildGroundEffect();
            setup.Flapping = spec.BuildFlapping();
            setup.RotorWake = spec.BuildRotorWake();
            setup.Modes = spec.BuildModes();
            const double h = WakeGridFixture.MapleTop;
            using var probe = new WindField(wind, new FlightRandom(Seed));
            Double3 downwind = WindField.Toward(probe.FromDegAt(start), 1);
            Double3 hold = new Double3(downwind.X * 2 * h, 0.75 * h, downwind.Z * 2 * h);
            setup.StartPosition = hold;
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, hold) { PositionIntegral = 0.1 };
            int steps = Seconds * 1000 - Warmup;
            var times = new long[steps];
            long allocated = 0, wall = Stopwatch.GetTimestamp();
            double tick = Stopwatch.Frequency / 1000.0;
            for (int k = 0; k < Warmup + steps; k++)
            {
                long due = wall + (long)(k * tick);
                while (Stopwatch.GetTimestamp() < due) { }
                StickSample stick = pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                long a = GC.GetAllocatedBytesForCurrentThread(), t0 = Stopwatch.GetTimestamp();
                core.Advance(stick);
                long t = Stopwatch.GetTimestamp() - t0;
                if (k < Warmup) continue;
                allocated += GC.GetAllocatedBytesForCurrentThread() - a;
                times[k - Warmup] = t;
            }
            Array.Sort(times);
            double us = 1e6 / Stopwatch.Frequency;
            double p50 = times[steps / 2] * us, p99 = times[steps * 99 / 100] * us, max = times[steps - 1] * us;
            var worker = core.Wind.Wakes;
            bool ok = p99 <= 300 && allocated == 0 && worker.Builds >= 2 && worker.Stalls == 0 && core.State.Armed && !core.State.Crashed;
            Console.WriteLine($"{Name}: Severe wind ({core.Wind.MeanSpeed:F2} m/s) in Severe rain ({core.Rain.RateMmPerHour:F1} mm/h) over the "
                + $"posadka, the whole airframe, held {hold.Y:F1} m up and {2 * h:F1} m downwind of the belt from second {start} (the bearing "
                + $"moving {turned:F1}° in {Seconds} s): {steps} steps p50 {p50:F2} µs, p99 {p99:F2} µs, max {max:F1} µs; the wake grid rebuilt "
                + $"{worker.Builds} times (last {worker.LastBuildSeconds * 1000:F0} ms on the worker), {worker.Stalls} stalls; allocation {allocated} B: "
                + $"{(ok ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B, ≥ 2 rebuilds, no stall)");
            return ok;
        }

        // The first second of the seed's Severe history from which the bearing moves Turn degrees or more within the flight.
        static int FirstTurn(out double turned)
        {
            using var field = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(Seed));
            for (int s = 0; s + Seconds <= field.Preset.Horizon; s++)
                for (int k = 1; k <= Seconds - 10; k++)
                {
                    turned = Math.Abs(field.FromDegAt(s + k) - field.FromDegAt(s));
                    if (turned >= Turn) return s;
                }
            throw new InvalidOperationException("seed " + Seed + "'s history never turns " + Turn + "° within " + Seconds + " s");
        }
    }
}
