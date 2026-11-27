using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // w20-hunter-track design 11, `--floor`: the pilot's "fly underneath a house and go full throttle" on FloorScenarios' timber floor,
    // run until something gives. With drone.json's 2 kg bottle on top (D-050) the bottle is pinned to the floor and the props stay
    // under it: the motors run at full duty until one fails by heat (MotorHealth), the drone falls and the props meet what they
    // fall onto. Without the bottle, and leaned about it, the props reach the floor within the first seconds. Then design 11's
    // addendum: without the bottle under the floor tilted 0, 1, 2, 5 and 15° (the props meet it within a second at every angle),
    // and the lead disc clamped 1 mm into the slab from 0 to 20°, with model 108's law beside it (no pass under about 3°). Fails on
    // a tunnel, on a blade pass before the first motor fails while the bottle holds the drone level, on no motor failing within the
    // run, or on an angle whose props never meet the floor.
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
            Console.WriteLine($"  the first motor failed by heat {failed - 1:F1} s after full throttle");
            foreach (double tilt in FloorScenarios.Tilts)
            {
                FloorScenarios.Pinned flat = FloorScenarios.UnderTheFloor(spec, 0, FloorScenarios.TiltSeconds, tilt: tilt);
                Console.WriteLine($"  no bottle, the floor at {tilt:F0}°, {FloorScenarios.TiltSeconds:F0} s: " + flat.Describe());
                ok &= flat.Tunnels == 0 && flat.FirstPassAt < 2;
            }
            foreach (double angle in new[] { 0.0, 0.5, 1, 2, 3, 5, 10, 15, 20 })
            {
                FloorScenarios.Press now = FloorScenarios.Pressed(spec, angle, 0.001, 1);
                FloorScenarios.Press was = FloorScenarios.Pressed(spec, angle, 0.001, 1, faces: false);
                Console.WriteLine($"  clamped 1 mm in at {angle:F1}°: " + now.Describe());
                Console.WriteLine($"    model 108's: {was.Passes} passes, {was.Jams} jams, the rotor at {was.MeanSpeed:F0} rad/s");
                ok &= now.Passes > 0;
            }
            Console.WriteLine($"  {clock.Elapsed.TotalSeconds:F0} s of wall time: {(ok ? "PASS" : "FAIL")}");
            return ok;
        }
    }
}
