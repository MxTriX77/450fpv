using System;
using System.Diagnostics;
using System.IO;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.FlightBench
{
    // The step as the pilot's build flies it (p7d-tether 1.13): the test range's houses' start ("track", D-077), legs on,
    // the app's loadout (1.0 kg, SessionSetup.Build's setup: the whole airframe, the map's own wind at Windy), and the fiber
    // laid from the table's launch behind the start, the coil full. The replay's scripted pilot (ScriptedFlight's: arm,
    // climb to 20 m, then 8 m/s round a 30 m circle with a 0.6 s reverse every 15 s), paced at 1 kHz of wall time as the
    // flight thread is. The bench times each step by phase (on the ground, the climb, the cruise) and counts the steps past
    // the 300 µs line and past the 1 ms period (the flight thread falling behind real time).
    internal static class HousesStepCase
    {
        public const string Name = "houses-line-step";

        const ulong Seed = 1;
        const int Seconds = 60;
        const double Cruise = 8, Radius = 30, Height = 20;

        public static FlightSetup Setup(out Double3 foot, out double stand, out Double3 nose)
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            SurfaceTable surfaces = SurfaceTable.Parse(File.ReadAllText(Path.Combine(RangeScenarios.Root, SurfaceTable.RelativePath)));
            var loadout = new Loadout { Payload = 1.0, Legs = true, CoilPaidOut = 0 };
            MapWorld map = RangeScenarios.World;
            StartPoint start = RangeScenarios.Start("track");
            ContactSpec contacts = spec.BuildContacts(loadout, materials, hash, surfaces);
            stand = 0;
            foreach (ContactShape s in contacts.Shapes)
                if (s.Role == ContactRole.Support) stand = Math.Max(stand, s.Radius - Math.Min(s.A.Y, s.B.Y));
            foot = start.Position;
            double heading = start.HeadingDeg * (Math.PI / 180);
            DetMath.SinCos(heading / 2, out double sin, out double cos);
            var attitude = new Quat(0, sin, 0, cos);
            DetMath.SinCos(heading, out sin, out cos);
            nose = new Double3(sin, 0, cos);
            return new FlightSetup
            {
                Seed = Seed,
                World = new FlightWorld(map),
                Mass = spec.BuildMass(loadout),
                Propulsion = spec.BuildPropulsion(),
                Aero = spec.BuildAero(),
                Wind = WindSetup.ForMap(map, WindLevel.Windy),
                Rain = RainLevel.Sunny,
                CameraPosition = spec.CameraPosition,
                Contacts = contacts,
                Asymmetry = spec.BuildAsymmetry(),
                Placement = spec.BuildPlacement(),
                VortexRing = spec.BuildVortexRing(),
                GroundEffect = spec.BuildGroundEffect(),
                RotorWake = spec.BuildRotorWake(),
                Flapping = spec.BuildFlapping(),
                Interference = spec.BuildInterference(),
                Modes = spec.BuildModes(),
                StartPosition = foot + new Double3(0, stand, 0),
                StartAttitude = attitude,
                Tether = TetherTable.Parse(File.ReadAllText(Path.Combine(RangeScenarios.Root, TetherTable.RelativePath))),
                TetherCoilPart = spec.Coil.Part,
                TetherSpoolMass = spec.Coil.SpoolMass,
            };
        }

        /// <summary>The replay's scripted pilot's sticks at t s (ScriptedFlight.Pilot without its landing).</summary>
        public static StickSample Sticks(SimPilot p, FlightCore core, Double3 foot, double stand, double t, long hostUs)
        {
            if (t < 0.5) return new StickSample { HostUs = hostUs, Arm = t >= 0.2 };
            if (t < 12) p.Target = foot + new Double3(0, stand + Math.Min(Height, 2 * (t - 0.5)), 0);
            else
            {
                double a = (t - 12) * Cruise / Radius;
                Double3 along = new Double3(Math.Cos(a), 0, Math.Sin(a)) * Cruise;
                if ((t - 12) % 15 >= 7 && (t - 12) % 15 < 7.6) along = along * -1;
                p.HoldVelocity = true;
                p.TargetVelocity = along;
                p.Target = foot + new Double3(0, stand + Height, 0);
            }
            StickSample s = p.Sticks(core, hostUs);
            s.Roll = Axis(s.Roll);
            s.Pitch = Axis(s.Pitch);
            s.Yaw = Axis(s.Yaw);
            s.Throttle = Math.Round(Math.Clamp(s.Throttle, 0, 1) * 1000) / 1000;
            return s;
        }

        static double Axis(double v) => Math.Round(Math.Clamp(v, -1, 1) * 500) / 500;

        public static bool Bench()
        {
            // The warm-up: the same flight's first 15 s unpaced, so the timed flight's first steps (on the
            // ground) run compiled code, as the app's own do after its loading screen; on .NET the tiered JIT would otherwise
            // charge its first compilations to the ground phase.
            FlightSetup setup = Setup(out Double3 foot, out double stand, out Double3 nose);
            var warm = new FlightCore(setup);
            var warmPilot = new SimPilot(warm.Controller, foot + new Double3(0, stand, 0)) { Period = 1, Heading = nose };
            for (int k = 0; k < 15000; k++) warm.Advance(Sticks(warmPilot, warm, foot, stand, k * 0.001, warm.State.Step * FlightCore.StepUs));
            warm.Wind.Dispose();

            setup = Setup(out foot, out stand, out nose);
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, foot + new Double3(0, stand, 0)) { Period = 1, Heading = nose };
            int steps = Seconds * 1000;
            var ground = new long[steps];
            var climb = new long[steps];
            var cruise = new long[steps];
            int ng = 0, nc = 0, nr = 0, over300 = 0, over1000 = 0;
            long allocated = 0, wall = Stopwatch.GetTimestamp(), late = 0;
            double tick = Stopwatch.Frequency / 1000.0, us = 1e6 / Stopwatch.Frequency;
            for (int k = 0; k < steps; k++)
            {
                long due = wall + (long)(k * tick);
                long now = Stopwatch.GetTimestamp();
                if (now > due + (long)tick) late++; // a step begun more than a period after it was due
                while (Stopwatch.GetTimestamp() < due) { }
                double t = k * 0.001;
                StickSample stick = Sticks(pilot, core, foot, stand, t, core.State.Step * FlightCore.StepUs);
                long a = GC.GetAllocatedBytesForCurrentThread(), t0 = Stopwatch.GetTimestamp();
                core.Advance(stick);
                long dt = Stopwatch.GetTimestamp() - t0;
                if (k >= 2000) allocated += GC.GetAllocatedBytesForCurrentThread() - a;
                if (dt * us > 300) over300++;
                if (dt * us > 1000) over1000++;
                if (t < 1.5) ground[ng++] = dt;
                else if (t < 12) climb[nc++] = dt;
                else cruise[nr++] = dt;
            }
            string P(long[] x, int n)
            {
                Array.Sort(x, 0, n);
                return $"p50 {x[n / 2] * us:F1} µs, p99 {x[Math.Min(n - 1, n * 99 / 100)] * us:F1} µs, max {x[n - 1] * us:F0} µs";
            }
            var all = new long[steps];
            Array.Copy(ground, 0, all, 0, ng);
            Array.Copy(climb, 0, all, ng, nc);
            Array.Copy(cruise, 0, all, ng + nc, nr);
            Array.Sort(all);
            double p99 = all[steps * 99 / 100] * us;
            FiberLine line = core.Tether;
            bool ok = p99 <= 300 && allocated == 0 && core.State.Armed && !core.State.Crashed;
            core.Wind.Dispose();
            Console.WriteLine($"{Name}: the houses' start (track), legs on, 1.0 kg, Windy map wind, the line ({line.PaidOut:F1} m paid out, "
                + $"{line.Chain.Count} nodes, {line.TrailCount} trail points, {line.Chain.Corrections} extra solves, {line.Chain.SafeSteps} safe steps), "
                + $"{steps} steps at 1 kHz: all {P(all, steps)}; on the ground (0–1.5 s) {P(ground, ng)}; the climb {P(climb, nc)}; "
                + $"the cruise {P(cruise, nr)}; {over300} steps over 300 µs, {over1000} over 1 ms, {late} begun a period late; "
                + (line.WindBatch == null ? "the nodes' wind on the flight thread; "
                    : $"the nodes' wind {line.WindBatch.ByWorker} points on the worker, {line.WindBatch.ByCaller} on the flight thread; ")
                + $"allocation {allocated} B: {(ok ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B)");
            return ok;
        }
    }
}
