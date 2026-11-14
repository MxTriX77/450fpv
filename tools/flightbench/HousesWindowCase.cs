using System;
using System.Diagnostics;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;

namespace Fpv450.FlightBench
{
    // The pilot's report (p7d-tether 1.13): the lag is worst "when I fly through the first house's window, inside this house".
    // The houses' start's setup (HousesStepCase.Setup: legs on, 1.0 kg, the whole airframe, the line laid behind), in the map's
    // Calm wind (the window leaves the drone about a decimetre a side), flown at the window's height through the yard fence's gap
    // (x −176.4…−174.0 at z −47.2, between two of its spans) and the first house's west window (the yard's Z01_house_adobe, its
    // north wall's opening x −175.0…−174.2 m, y 0.94…1.95 m, the wall z −51.46…−51.00 m) into its west room (x −176.55…−174.02,
    // z −56.54…−51.46, ceiling 2.41 m), and held there swaying slowly across it with the line through the window. The geometry is the
    // map's (the shapes' boxes). Not over the fence and down to the window: a first try that way broke the line behind the fence,
    // before the window (not traced; the bench wants the line whole). Paced at 1 kHz as HousesStepCase is; the
    // phases: on the ground, the approach (up to the window's height, to the gap), the window (the carrot through it), and
    // inside the house.
    internal static class HousesWindowCase
    {
        public const string Name = "houses-window-step";

        const int Seconds = 60;
        // The window's middle (x, y), the point before the fence's gap and the room's (assumed, from the boxes above).
        const double WindowX = -174.6, WindowY = 1.45, Before = -45.0, Inside = -54.0;
        // Inside: a sway across the room, never back toward the window (a first try's circle, 0.5 m round, broke the line 10 s in;
        // not traced), its middle, half-width, rate and height (assumed: 0.8 m from the side walls).
        const double RoomX = -175.3, RoomZ = -54.5, RoomSway = 0.45, RoomRate = 0.4, RoomY = 1.3;
        const double TApproach = 4, TSettle = 10, TThrough = 12, TInside = 17.5;

        public static FlightSetup Setup(out Double3 foot, out double stand, out Double3 nose)
        {
            FlightSetup setup = HousesStepCase.Setup(out foot, out stand, out nose);
            setup.Wind = WindSetup.ForMap(RangeScenarios.World, WindLevel.Calm);
            return setup;
        }

        // The carrot at t s (the pilot's target; the nose south toward the window from the approach on).
        static Double3 Carrot(Double3 start, double stand, double t)
        {
            if (t < TApproach) return start + new Double3(0, stand + Math.Min(WindowY - stand, 0.5 * (t - 0.5)), 0);
            Double3 up = new Double3(start.X, WindowY, start.Z), gap = new Double3(WindowX, WindowY, Before);
            if (t < TThrough) return up + (gap - up) * Math.Min(1, (t - TApproach) / (TSettle - TApproach));
            if (t < TInside) return new Double3(WindowX, WindowY, Before + (Inside - Before) * Math.Min(1, (t - TThrough) / (TInside - TThrough - 0.5)));
            // From where the carrot stopped (x −174.6) to the sway's middle over 2 s, then across and back.
            double settle = Math.Min(1, (t - TInside) / 2);
            return new Double3(WindowX + (RoomX - WindowX) * settle + RoomSway * settle * Math.Sin(RoomRate * (t - TInside)), RoomY, Inside + (RoomZ - Inside) * settle);
        }
        public static StickSample Sticks(SimPilot p, FlightCore core, Double3 start, double stand, double t, long hostUs)
        {
            if (t < 0.5) return new StickSample { HostUs = hostUs, Arm = t >= 0.2 };
            p.Target = Carrot(start, stand, t);
            if (t >= TApproach) p.Heading = new Double3(0, 0, -1);
            StickSample s = p.Sticks(core, hostUs);
            s.Roll = Axis(s.Roll);
            s.Pitch = Axis(s.Pitch);
            s.Yaw = Axis(s.Yaw);
            s.Throttle = Math.Round(Math.Clamp(s.Throttle, 0, 1) * 1000) / 1000;
            return s;
        }

        // The scripted pilot, stiffer in position than the circle's (a window is a decimetre's margin): ω_n 1.2 rad/s, ζ 0.8, no
        // integrator (Calm holds no steady lean). Assumed, for the bench's flight only.
        public static SimPilot Pilot(FlightCore core, Double3 foot, double stand, Double3 nose) =>
            new SimPilot(core.Controller, foot + new Double3(0, stand, 0)) { Period = 1, Heading = nose, PositionGain = 1.5, VelocityGain = 2.0, PositionIntegral = 0 };

        static double Axis(double v) => Math.Round(Math.Clamp(v, -1, 1) * 500) / 500;

        public static bool Bench()
        {
            // The warm-up: the same flight's first 15 s unpaced (HousesStepCase.Bench's reason: the tiered JIT).
            FlightSetup setup = Setup(out Double3 foot, out double stand, out Double3 nose);
            var warm = new FlightCore(setup);
            SimPilot warmPilot = Pilot(warm, foot, stand, nose);
            for (int k = 0; k < 15000; k++) warm.Advance(Sticks(warmPilot, warm, foot, stand, k * 0.001, warm.State.Step * FlightCore.StepUs));
            warm.Wind.Dispose();

            setup = Setup(out foot, out stand, out nose);
            var core = new FlightCore(setup);
            SimPilot pilot = Pilot(core, foot, stand, nose);
            int steps = Seconds * 1000;
            // 0 the ground, 1 the approach, 2 the window, 3 inside the house, 4 after the break or the crash.
            var phase = new long[5][];
            var count = new int[5];
            for (int p = 0; p < 5; p++) phase[p] = new long[steps];
            var all = new long[steps];
            int over300 = 0, over1000 = 0;
            var newton = new long[5];
            var newtonSteps = new long[5];
            long newtonMax = 0;
            long crashed = -1, allocated = 0, wall = Stopwatch.GetTimestamp(), late = 0;
            double tick = Stopwatch.Frequency / 1000.0, us = 1e6 / Stopwatch.Frequency;
            for (int k = 0; k < steps; k++)
            {
                long due = wall + (long)(k * tick);
                if (Stopwatch.GetTimestamp() > due + (long)tick) late++;
                while (Stopwatch.GetTimestamp() < due) { }
                double t = k * 0.001;
                bool whole = !core.Tether.Broken && crashed < 0;
                StickSample stick = Sticks(pilot, core, foot, stand, t, core.State.Step * FlightCore.StepUs);
                long solves = core.Tether.Chain.NewtonSolves;
                long a = AllocProbe.Before(), t0 = Stopwatch.GetTimestamp();
                core.Advance(stick);
                long dt = Stopwatch.GetTimestamp() - t0;
                if (k >= 2000) allocated += AllocProbe.After(a);
                solves = core.Tether.Chain.NewtonSolves - solves; // model 77's segments' Newton solves this step
                if (solves > newtonMax) newtonMax = solves;
                if (crashed < 0 && core.State.Crashed) crashed = core.State.Step;
                if (dt * us > 300) over300++;
                if (dt * us > 1000) over1000++;
                int p = !whole ? 4 : t < 1.5 ? 0 : t < TThrough ? 1 : t < TInside ? 2 : 3;
                phase[p][count[p]++] = dt;
                newton[p] += solves;
                if (solves > 0) newtonSteps[p]++;
                all[k] = dt;
            }
            string P(long[] x, int n)
            {
                if (n == 0) return "none";
                Array.Sort(x, 0, n);
                return $"p50 {x[n / 2] * us:F1} µs, p99 {x[Math.Min(n - 1, n * 99 / 100)] * us:F1} µs, max {x[n - 1] * us:F0} µs";
            }
            Array.Sort(all);
            double p99 = all[steps * 99 / 100] * us;
            FiberLine line = core.Tether;
            Double3 end = core.State.Position;
            bool inside = end.X > -176.55 && end.X < -174.02 && end.Z > -56.54 && end.Z < -51.46 && end.Y < 2.41;
            bool ok = p99 <= 300 && allocated == 0 && core.State.Armed && !core.State.Crashed;
            core.Wind.Dispose();
            Console.WriteLine($"{Name}: the houses' start (track), legs on, 1.0 kg, Calm map wind, through the fence's gap and the first house's west "
                + $"window into its room, the line ({line.PaidOut:F1} m paid out, {line.Chain.Count} nodes, {line.TrailCount} trail points, "
                + $"{line.Chain.Corrections} extra solves, {line.Chain.SafeSteps} safe steps, {line.Contacts.Touching} nodes touching at the end), "
                + (line.Broken ? $"the line broke at step {line.BrokenAt} ({line.LastBreak.Mechanism}), " : "the line whole, ")
                + (crashed >= 0 ? $"the drone crashed at step {crashed}, " : "")
                + $"the drone {(inside ? "inside the room" : "not in the room")} at the end, {steps} steps at 1 kHz: all {P(all, steps)}; "
                + $"on the ground (0–1.5 s) {P(phase[0], count[0])}; the approach {P(phase[1], count[1])}; the window ({TThrough}–{TInside} s) "
                + $"{P(phase[2], count[2])}; inside the house ({count[3]} steps) {P(phase[3], count[3])}; Newton solves (model 77's) on the ground, the approach, "
                + $"the window, inside and after: {newton[0]}, {newton[1]}, {newton[2]}, {newton[3]}, {newton[4]} (inside {(count[3] > 0 ? (double)newton[3] / count[3] : 0):F3} a step, "
                + $"{newtonSteps[3]} steps with one or more), at most {newtonMax} a step, {line.Chain.NewtonUnconverged} steps past the budget; after the break or the crash "
                + $"({count[4]} steps) {P(phase[4], count[4])}; {over300} steps over 300 µs, {over1000} over 1 ms, {late} begun a period late; "
                + (core.WindBatch == null ? "the step's wind on the flight thread; "
                    : $"the step's wind {core.WindBatch.ByWorker} points on the worker, {core.WindBatch.ByCaller} on the flight thread; ")
                + $"allocation {allocated} B: {(ok ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B)");
            return ok;
        }
    }
}
