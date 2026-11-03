using System;
using System.Diagnostics;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

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
    // map-step (p7c-wind, the map's wind): the same over the test range in its own wind (WindSetup.ForMap), held 2 m inside
    // the belt's west section's north edge at 0.75 h, where a point takes the canopy's share and the wake grid's both.
    // map-cut-step (p7c-wind 1.10, the wind inside a cut): the same on the test range's own world (the ground under the body
    // from SampleGround and SoilUnder, the rotors' rays on the map), held 0.75 m over E01's run floor in the middle of a
    // straight leg, so every point of the step is in the cut's frame; then the field alone at the same frame, with the
    // cuts and without them (model 26's field), for the cavity's own cost a step.
    internal static class PosadkaStepCase
    {
        public const string Name = "posadka-step", MapName = "map-step", CutName = "map-cut-step";

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
            setup.RotorWake = spec.BuildRotorWake();
            setup.Flapping = spec.BuildFlapping();
            setup.Interference = spec.BuildInterference();
            setup.Modes = spec.BuildModes();
            const double h = WakeGridFixture.MapleTop;
            using var probe = new WindField(wind, new FlightRandom(Seed));
            Double3 downwind = WindField.Toward(probe.FromDegAt(start), 1);
            Double3 hold = new Double3(downwind.X * 2 * h, 0.75 * h, downwind.Z * 2 * h);
            return Run(Name, setup, hold, start, turned, $"posadka, the whole airframe, held {hold.Y:F1} m up and {2 * h:F1} m downwind of the belt");
        }

        public static bool BenchMap()
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            int start = FirstTurn(out double turned);
            MapWorld map = RangeScenarios.World;
            WindSetup wind = WindSetup.ForMap(map, WindLevel.Severe);
            wind.StartSecond = start;
            FlightSetup setup = CrosswindScenario.Setup(spec, wind, Seed);
            setup.Rain = RainLevel.SevereRain;
            setup.Asymmetry = spec.BuildAsymmetry();
            setup.VortexRing = spec.BuildVortexRing();
            setup.GroundEffect = spec.BuildGroundEffect();
            setup.RotorWake = spec.BuildRotorWake();
            setup.Flapping = spec.BuildFlapping();
            setup.Interference = spec.BuildInterference();
            setup.Modes = spec.BuildModes();
            CanopyStrip west = MapWind.Of(map).Belts[0].Strip;
            var hold = new Double3(west.CentreX, 0.75 * west.Height, west.CentreZ + west.Width / 2 - 2);
            return Run(MapName, setup, hold, start, turned, $"test range in its own wind, the whole airframe, held {hold.Y:F1} m up 2 m inside the belt's edge");
        }

        public static bool BenchCut()
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            int start = FirstTurn(out double turned);
            MapWorld map = RangeScenarios.World;
            WindSetup wind = WindSetup.ForMap(map, WindLevel.Severe);
            wind.StartSecond = start;
            FlightSetup setup = CrosswindScenario.Setup(spec, wind, Seed);
            setup.World = new FlightWorld(map);
            setup.Rain = RainLevel.SevereRain;
            setup.Asymmetry = spec.BuildAsymmetry();
            setup.VortexRing = spec.BuildVortexRing();
            setup.GroundEffect = spec.BuildGroundEffect();
            setup.RotorWake = spec.BuildRotorWake();
            setup.Flapping = spec.BuildFlapping();
            setup.Interference = spec.BuildInterference();
            setup.Modes = spec.BuildModes();
            var hold = new Double3(61.654, -0.75, -14.0875); // E01's leg between its centre line's points 24 and 26
            bool ok = Run(CutName, setup, hold, start, turned, $"test range's own world and wind, the whole airframe, held {hold.Y + 1.5:F2} m over E01's run floor",
                core => core.Wind.Cut.Active && !core.Wind.Cut.Covered && core.State.Position.Y < core.Wind.Cut.Lip);

            // The field alone at the same frame: Begin at the hold and At at the four hubs, the body and the coil, unpaced.
            PropulsionSpec propulsion = spec.BuildPropulsion();
            var points = new Double3[6];
            for (int i = 0; i < 4; i++) points[i] = hold + propulsion.Motors[i].Position;
            points[4] = hold;
            points[5] = hold + new Double3(0, -0.06, 0);
            const double floor = -1.5;
            WindSetup open = wind;
            open.Cuts = null;
            double withCuts = FieldStep(wind, hold, floor, points), without = FieldStep(open, hold, floor, points);
            Console.WriteLine($"{CutName}: the field alone at that frame (Begin and 6 points, 58 000 steps, unpaced, the best of 3): {withCuts:F2} µs a step "
                + $"with the cut, {without:F2} µs without (model 26's field over the floor): the cavity {withCuts - without:+0.00;-0.00} µs a step");
            return ok;
        }

        // The field's mean cost a step, µs: Begin at the origin over its ground, then At at each point (the best of three runs).
        static double FieldStep(WindSetup wind, Double3 origin, double ground, Double3[] points)
        {
            double best = double.PositiveInfinity;
            for (int run = 0; run < 3; run++)
                using (var field = new WindField(wind, new FlightRandom(Seed)))
                {
                    const int steps = 58000;
                    Double3 sum = default;
                    for (int k = 0; k < Warmup; k++)
                    {
                        field.Begin(k, origin, ground);
                        foreach (Double3 p in points) sum = sum + field.At(p);
                    }
                    long t0 = Stopwatch.GetTimestamp();
                    for (int k = Warmup; k < Warmup + steps; k++)
                    {
                        field.Begin(k, origin, ground);
                        foreach (Double3 p in points) sum = sum + field.At(p);
                    }
                    double us = (Stopwatch.GetTimestamp() - t0) * 1e6 / Stopwatch.Frequency / steps;
                    if (double.IsNaN(sum.X)) throw new InvalidOperationException("the field read NaN");
                    best = Math.Min(best, us);
                }
            return best;
        }

        static bool Run(string name, FlightSetup setup, Double3 hold, int start, double turned, string where, Func<FlightCore, bool> inside = null)
        {
            setup.StartPosition = hold;
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, hold) { PositionIntegral = 0.1 };
            int steps = Seconds * 1000 - Warmup;
            var times = new long[steps];
            long allocated = 0, wall = Stopwatch.GetTimestamp();
            int held = 0;
            double tick = Stopwatch.Frequency / 1000.0;
            for (int k = 0; k < Warmup + steps; k++)
            {
                long due = wall + (long)(k * tick);
                while (Stopwatch.GetTimestamp() < due) { }
                StickSample stick = pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                long a = GC.GetAllocatedBytesForCurrentThread(), t0 = Stopwatch.GetTimestamp();
                core.Advance(stick);
                long t = Stopwatch.GetTimestamp() - t0;
                if (inside != null && inside(core)) held++;
                if (k < Warmup) continue;
                allocated += GC.GetAllocatedBytesForCurrentThread() - a;
                times[k - Warmup] = t;
            }
            Array.Sort(times);
            double us = 1e6 / Stopwatch.Frequency;
            double p50 = times[steps / 2] * us, p99 = times[steps * 99 / 100] * us, max = times[steps - 1] * us;
            var worker = core.Wind.Wakes;
            bool ok = p99 <= 300 && allocated == 0 && worker.Builds >= 2 && worker.Stalls == 0 && core.State.Armed && !core.State.Crashed
                && (inside == null || held == Seconds * 1000);
            Console.WriteLine($"{name}: Severe wind ({core.Wind.MeanSpeed:F2} m/s) in Severe rain ({core.Rain.RateMmPerHour:F1} mm/h) over the "
                + $"{where} from second {start} (the bearing "
                + $"moving {turned:F1}° in {Seconds} s): {steps} steps p50 {p50:F2} µs, p99 {p99:F2} µs, max {max:F1} µs; the wake grid rebuilt "
                + $"{worker.Builds} times (last {worker.LastBuildSeconds * 1000:F0} ms on the worker), {worker.Stalls} stalls; allocation {allocated} B"
                + (inside == null ? "" : $"; {held} of {Seconds * 1000} steps in the cut below its lip") + ": "
                + $"{(ok ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B, ≥ 2 rebuilds, no stall{(inside == null ? "" : ", every step in the cut")})");
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
