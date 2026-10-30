using System;
using System.Diagnostics;
using System.IO;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;

namespace Fpv450.FlightBench
{
    // The powered flight (tasks 1.4 and 1.5): the repository's drone with the Betaflight 4.5 controller at 4 kHz and the
    // ESCs, motors, props and packs at its loops, its drag and the Windy stand-in wind, flown by GoldenFlights' scripted pilot (the same file the suite compiles,
    // so the suite on .NET and on Unity's Mono and this Release build pin one hash). The bench times the powered step.
    internal static class PoweredFlightCase
    {
        public const string Name = "powered-flight";

        public static DroneSpec Drone() => DroneSpec.Parse(DroneText());

        /// <summary>The repository's drone.json, found from the working directory up.</summary>
        public static string DroneText()
        {
            for (string dir = Directory.GetCurrentDirectory(); dir != null; dir = Path.GetDirectoryName(dir))
            {
                string path = Path.Combine(dir, "assets-src", "drone", "drone.json");
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            throw new FileNotFoundException("assets-src/drone/drone.json was not found from " + Directory.GetCurrentDirectory());
        }

        public static bool Golden()
        {
            ulong hash = GoldenFlights.FlyPowered(Drone(), out FlightCore core);
            // The audit: model 6's drone (1.5's rotor drag) in still air over model 5's 1296-byte layout flies model 5's
            // pin (p7c-wind 1.4 and 1.6).
            DroneSpec model6 = GoldenFlights.Model6Drone(DroneText());
            int skip = LogFormat.StepSize - GoldenFlights.Model5Bytes;
            ulong still = GoldenFlights.FlyPowered(model6, out _, GoldenFlights.Model5Bytes, skip, WindSetup.Of(WindLevel.Still));
            bool ok = hash == GoldenFlights.PoweredHash && still == GoldenFlights.StillAirPoweredHash && core.State.Armed;
            Console.WriteLine($"{Name}: {GoldenFlights.PoweredSteps} steps, hash 0x{hash:X16} "
                + $"({(hash == GoldenFlights.PoweredHash ? "matches" : $"golden 0x{GoldenFlights.PoweredHash:X16}")}), armed {core.State.Armed}, "
                + $"packs at {core.State.PackCharge0:F4} of 0.9; model 6's drone in still air over model 5's 1296-byte layout 0x{still:X16} "
                + $"({(still == GoldenFlights.StillAirPoweredHash ? "model 5's" : $"model 5's is 0x{GoldenFlights.StillAirPoweredHash:X16}")}): {(ok ? "PASS" : "FAIL")}");
            ulong contact = GoldenFlights.FlyContact(Drone(), out FlightCore c);
            bool cok = contact == GoldenFlights.ContactHash && c.Contacts.ImpactCount >= 2 && !c.State.Crashed;
            Console.WriteLine($"contact-flight: {GoldenFlights.ContactSteps} steps, hash 0x{contact:X16} "
                + $"({(contact == GoldenFlights.ContactHash ? "matches" : $"golden 0x{GoldenFlights.ContactHash:X16}")}), {c.Contacts.ImpactCount} impacts, "
                + $"ends {c.State.Position.Y:F4} m up on {c.Trace.ContactsTouching} touching pairs: {(cok ? "PASS" : "FAIL")}");
            return ok && cok;
        }

        public static bool Bench()
        {
            const int warmup = 2000, steps = 60000; // 60 s of flight: the scripted pilot's 10 s six times
            var core = new FlightCore(GoldenFlights.PoweredSetup(Drone()));
            var sticks = new StickSample[GoldenFlights.PoweredSteps];
            for (int k = 0; k < sticks.Length; k++) sticks[k] = GoldenFlights.PoweredStick(k);
            var times = new long[steps];
            for (int k = 0; k < warmup; k++) core.Advance(sticks[k]);

            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < steps; k++)
            {
                StickSample stick = sticks[(warmup + k) % sticks.Length];
                if (stick.HostUs < 20 * FlightCore.StepUs) stick.Arm = true; // the loop's restart keeps the switch on
                long t0 = Stopwatch.GetTimestamp();
                core.Advance(stick);
                times[k] = Stopwatch.GetTimestamp() - t0;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;

            Array.Sort(times);
            double us = 1e6 / Stopwatch.Frequency;
            double p50 = times[steps / 2] * us, p99 = times[steps * 99 / 100] * us, max = times[steps - 1] * us;
            bool ok = p99 <= 300 && allocated == 0 && core.State.Armed;
            Console.WriteLine($"{Name}: {steps} steps of {FlightController.LoopsPerStep} controller loops and propulsion sub-steps, "
                + $"p50 {p50:F2} µs, p99 {p99:F2} µs, max {max:F1} µs, steady-state allocation {allocated} B: {(ok ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B)");
            return ok;
        }
    }
}
