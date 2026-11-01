using System;
using System.Diagnostics;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // The budget with every airframe effect on (p7b-airframe 1.6, PLT-04): the motors' asymmetry, the vortex rings, the
    // ground and ceiling effect with its recirculation, the flapping and the airframe's modes, with the battery's sag (always
    // on), in each effect's worst case: on the stand (legs off at idle, eight arm edges on four posts, the rotors' rays on
    // the stand), in the close hover (legs on, the pilot's target the skids 0.1 m up, GroundEffectScenario's: the cushion,
    // the wash and the contacts' margin) and in the vortex ring (6.2 kg held sinking at 8 m/s, the rings latched). Each times
    // the step with them on and off (their share of it), the world queries' share, and the flight thread's allocation.
    internal static class AirframeCase
    {
        public const string Name = "airframe";

        enum Case { Stand, CloseHover, VortexRing }

        /// <summary>The ring's case: the start's height, m; the sink held, m/s (η 0.72 at the weight's v_h of 11.04 m/s, past
        /// the entry's 0.454 and inside the loss from 0.61); the hover before it and the ramp into it, s.</summary>
        const double Top = 700, Sink = 8, HoverBefore = 2, Ramp = 2;

        const int Steps = 60000;

        /// <summary>One flight's sticks: armed at idle on the stand, the closed-loop pilot otherwise.</summary>
        sealed class Flight
        {
            readonly Case _case;
            readonly double _stand;
            SimPilot _pilot;

            public Flight(Case c, double stand)
            {
                _case = c;
                _stand = stand;
            }

            public StickSample Stick(FlightCore core, int k)
            {
                long us = core.State.Step * FlightCore.StepUs;
                if (k < 16 || _case == Case.Stand) return new StickSample { HostUs = us, Arm = k >= 8 };
                if (_pilot == null)
                    _pilot = _case == Case.CloseHover
                        ? new SimPilot(core.Controller, new Double3(0, _stand + GroundEffectScenario.HoverLift, 0)) { ThrottleBase = GroundEffectScenario.HoverThrottle }
                        : new SimPilot(core.Controller, new Double3(0, Top, 0));
                if (_case == Case.VortexRing)
                {
                    double t = (k - 16) * FlightCore.Dt - HoverBefore;
                    double depth = t <= 0 ? 0 : t < Ramp ? Sink * t * t / (2 * Ramp) : Sink * (t - Ramp / 2);
                    _pilot.Target = new Double3(0, Top - depth, 0);
                }
                return _pilot.Sticks(core, us);
            }
        }

        static FlightSetup Setup(DroneSpec spec, Case c, bool on, out double stand, out int warmup)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            FlightSetup s;
            stand = 0;
            switch (c)
            {
                case Case.Stand:
                    Loadout loadout = spec.Nominal;
                    loadout.Legs = false;
                    ContactSpec contacts = spec.BuildContacts(loadout, materials, hash);
                    s = ContactScenarios.Setup(spec, materials, hash, false, LaunchStand.RestingOrigin(spec, contacts, default));
                    s.RuntimeObjects = new[] { LaunchStand.Build(spec, default, 0, s.World.Query.MaterialId(spec.Stand.Material)) };
                    warmup = 2000;
                    break;
                case Case.CloseHover:
                    s = GroundEffectScenario.HoverSetup(spec, true, out stand);
                    warmup = (int)(GroundEffectScenario.SettleSeconds / FlightCore.Dt);
                    break;
                default:
                    s = VortexRingScenario.Setup(spec);
                    s.Contacts = spec.BuildContacts(new Loadout { Payload = 2.0, Legs = true }, materials, hash);
                    s.StartPosition = new Double3(0, Top, 0);
                    warmup = 16 + (int)((HoverBefore + Ramp + 2) / FlightCore.Dt);
                    break;
            }
            s.Asymmetry = on ? spec.BuildAsymmetry() : null;
            s.VortexRing = on ? spec.BuildVortexRing() : null;
            s.GroundEffect = on ? spec.BuildGroundEffect() : null;
            s.Flapping = on ? spec.BuildFlapping() : null;
            s.Modes = on ? spec.BuildModes() : null;
            return s;
        }

        /// <summary>One flight timed step by step; the flights with the effects on and off take turns a second at a time, so a
        /// load on the host (other agents' builds) falls on both alike.</summary>
        sealed class Run
        {
            const int Block = 1000;
            readonly FlightCore _core;
            readonly Flight _flight;
            readonly double _stand;
            readonly int _warmup;
            readonly long[] _times = new long[Steps];
            int _k, _any, _all;
            double _clear;

            public Run(DroneSpec spec, Case c, bool on)
            {
                _core = new FlightCore(Setup(spec, c, on, out _stand, out _warmup));
                _flight = new Flight(c, _stand);
                for (int k = 0; k < _warmup; k++) _core.Advance(_flight.Stick(_core, k));
            }

            public static long Interleave(Run a, Run b)
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int block = 0; block < Steps / Block; block++)
                {
                    a.Step(Block);
                    b.Step(Block);
                }
                return GC.GetAllocatedBytesForCurrentThread() - allocated;
            }

            void Step(int n)
            {
                for (int i = 0; i < n; i++, _k++)
                {
                    StickSample stick = _flight.Stick(_core, _warmup + _k);
                    long t0 = Stopwatch.GetTimestamp();
                    _core.Advance(stick);
                    _times[_k] = Stopwatch.GetTimestamp() - t0;
                    byte rings = _core.State.RingLatched;
                    if (rings != 0) _any++;
                    if (rings == 15) _all++;
                    _clear += _core.State.Position.Y - _stand;
                }
            }

            public double P50, P99, Max, InRing, AllRings, Clearance;
            public int Touching;
            public bool Flying;

            public void Sum()
            {
                Array.Sort(_times);
                double us = 1e6 / Stopwatch.Frequency;
                P50 = _times[Steps / 2] * us;
                P99 = _times[Steps * 99 / 100] * us;
                Max = _times[Steps - 1] * us;
                InRing = (double)_any / Steps;
                AllRings = (double)_all / Steps;
                Clearance = _clear / Steps;
                Touching = _core.Trace.ContactsTouching;
                Flying = _core.State.Armed && !_core.State.Crashed;
            }
        }

        public static bool Bench()
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            bool ok = true;
            foreach (Case c in new[] { Case.Stand, Case.CloseHover, Case.VortexRing })
            {
                Run on = new Run(spec, c, true), off = new Run(spec, c, false);
                long allocated = Run.Interleave(on, off);
                on.Sum();
                off.Sum();
                FlightSetup again = Setup(spec, c, true, out double stand, out int warmup);
                var flight = new Flight(c, stand);
                string share = TimedQuery.Share(again, flight.Stick, warmup, Steps);
                string where = c == Case.Stand ? $"on the stand, legs off at idle, {on.Touching} touching pairs"
                    : c == Case.CloseHover ? $"the close hover, legs on, the skids {on.Clearance:F3} m up on average"
                    : $"in the vortex ring, 6.2 kg sinking at {Sink:F0} m/s, a ring latched {on.InRing * 100:F1} % of the steps, all four {on.AllRings * 100:F1} %";
                bool held = c == Case.Stand ? on.Touching > 0 : c == Case.CloseHover ? on.Clearance > 0 && on.Clearance < 0.2 : on.AllRings > 0.9;
                bool pass = on.P99 <= 300 && allocated == 0 && on.Flying && held;
                ok &= pass;
                Console.WriteLine($"{Name}: {where}: {Steps} steps with every airframe effect on p50 {on.P50:F2} µs, p99 {on.P99:F2} µs, max {on.Max:F1} µs; "
                    + $"off, in turn with it, p50 {off.P50:F2} µs, p99 {off.P99:F2} µs; the effects {on.P50 - off.P50:+0.00;-0.00} µs at p50 "
                    + $"({100 * (on.P50 - off.P50) / on.P50:F1} % of the step); steady-state allocation of both {allocated} B; {share}: "
                    + $"{(pass ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B)");
            }
            return ok;
        }
    }
}
