using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // w20-hunter-track design 11, `--floor`: the pilot's "fly underneath a house and go full throttle" on FloorScenarios' timber floor,
    // run until something gives. With drone.json's 2 kg bottle on top (D-050) the bottle is pinned to the floor and the props stay
    // under it: the motors run at full duty until one fails by heat (MotorHealth), the drone falls and the props meet what they
    // fall onto. Without the bottle, and leaned about it, the props reach the floor within the first seconds. Fails on a tunnel,
    // on a blade pass before the first motor fails while the bottle holds the drone level, or on no motor failing within the run.
    internal static class FloorCase
    {
        public static bool Run(double seconds)
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            FloorScenarios.Pinned pinned = FloorScenarios.UnderTheFloor(spec, FloorScenarios.PilotPayload, seconds);
            Console.WriteLine($"  {FloorScenarios.PilotPayload:F1} kg bottle, level, {seconds:F0} s: " + pinned.Describe());
            FloorScenarios.Pinned bare = FloorScenarios.UnderTheFloor(spec, 0, 10);
            Console.WriteLine("  no bottle, level, 10 s: " + bare.Describe());
            FloorScenarios.Pinned leaned = FloorScenarios.UnderTheFloor(spec, FloorScenarios.PilotPayload, 10, roll: 0.2);
            Console.WriteLine($"  {FloorScenarios.PilotPayload:F1} kg bottle, roll stick 0.2 held, 10 s: " + leaned.Describe());
            double failed = pinned.FirstFailure();
            bool ok = pinned.Tunnels + bare.Tunnels + leaned.Tunnels == 0 && failed < seconds && pinned.PassesBeforeFailure == 0 && bare.Passes > 0 && leaned.Passes > 0;
            Console.WriteLine($"  the first motor failed by heat {failed - 1:F1} s after full throttle; {clock.Elapsed.TotalSeconds:F0} s of wall time: {(ok ? "PASS" : "FAIL")}");
            return ok;
        }
    }
}
