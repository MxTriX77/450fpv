using System;
using System.Diagnostics;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.FlightBench
{
    // A new flight's build as the app makes one on R, Fly or a restart (p6-flight-core, the build's freeze): the houses'
    // start with SessionSetup.Build's setup (HousesStepCase.Setup: the map's own wind at Windy, its wakes, belt and cuts,
    // the fiber laid). Times, on this thread, the setup (WindSetup.ForMap and the rest) and the FlightCore constructor, for
    // the process's first flight, restarts with the same seed (the same drawn bearing), and flights with new seeds (the
    // player's R: a new seed, a new bearing), then the build's parts once. Numbers only: the host is shared, so nothing
    // here is a pass or a fail.
    internal static class BuildCase
    {
        public const string Name = "flight-build";

        public static bool Bench()
        {
            double ms = 1000.0 / Stopwatch.Frequency;
            long t0 = Stopwatch.GetTimestamp();
            MapWorld map = RangeScenarios.World;
            Console.WriteLine($"  map load (RangeScenarios.World, first touch): {(Stopwatch.GetTimestamp() - t0) * ms:F0} ms");

            Build("first flight, seed 1", 1);
            for (int i = 0; i < 3; i++) Build("restart, seed 1", 1);
            for (int i = 0; i < 3; i++) Build("new seed " + (100 + i), (ulong)(100 + i));

            // The parts, once each, warm.
            FlightSetup setup = HousesStepCase.Setup(out _, out _, out _);
            WakeLayout layout = setup.Wind.Wakes;
            t0 = Stopwatch.GetTimestamp();
            WindSetup.ForMap(map, WindLevel.Windy);
            Console.WriteLine($"  part: WindSetup.ForMap {(Stopwatch.GetTimestamp() - t0) * ms:F1} ms");
            t0 = Stopwatch.GetTimestamp();
            WakeObstacles obstacles = layout.Obstacles();
            Console.WriteLine($"  part: WakeLayout.Obstacles {(Stopwatch.GetTimestamp() - t0) * ms:F1} ms ({obstacles.NX} x {obstacles.NZ} cells, {layout.Volumes.Length} volumes)");
            t0 = Stopwatch.GetTimestamp();
            WakeGrid.Build(obstacles, 211.3);
            Console.WriteLine($"  part: one WakeGrid.Build {(Stopwatch.GetTimestamp() - t0) * ms:F1} ms");
            setup.Seed = 200;
            t0 = Stopwatch.GetTimestamp();
            using (new WindField(setup.Wind, new FlightRandom(setup.Seed))) { }
            Console.WriteLine($"  part: WindField, seed 200 {(Stopwatch.GetTimestamp() - t0) * ms:F1} ms");
            WindSetup noWakes = setup.Wind;
            noWakes.Wakes = null;
            t0 = Stopwatch.GetTimestamp();
            using (new WindField(noWakes, new FlightRandom(setup.Seed))) { }
            Console.WriteLine($"  part: WindField without the wakes, seed 200 {(Stopwatch.GetTimestamp() - t0) * ms:F1} ms");
            setup.Seed = 201;
            setup.Tether = null;
            t0 = Stopwatch.GetTimestamp();
            var bare = new FlightCore(setup);
            Console.WriteLine($"  part: FlightCore without the fiber, seed 201 {(Stopwatch.GetTimestamp() - t0) * ms:F1} ms");
            bare.Wind.Dispose();
            return true;
        }

        static void Build(string what, ulong seed)
        {
            double ms = 1000.0 / Stopwatch.Frequency;
            long t0 = Stopwatch.GetTimestamp();
            FlightSetup setup = HousesStepCase.Setup(out _, out _, out _);
            setup.Seed = seed;
            long t1 = Stopwatch.GetTimestamp();
            var core = new FlightCore(setup);
            long t2 = Stopwatch.GetTimestamp();
            Console.WriteLine($"  {what}: setup {(t1 - t0) * ms:F0} ms, FlightCore {(t2 - t1) * ms:F0} ms, total {(t2 - t0) * ms:F0} ms");
            core.Wind.Dispose();
        }
    }
}
