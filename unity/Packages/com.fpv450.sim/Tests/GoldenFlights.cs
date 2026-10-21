using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The powered golden flight (p6-flight-core 1.4): the repository's drone at its nominal loadout, armed by
    /// Betaflight's rules and flown 10 s by a scripted pilot through every part of the controller and propulsion (a
    /// spin-up, a climb, roll, pitch and yaw steps, a throttle punch and chop, a weave). Its hash is every step's log
    /// record and events, so it pins the whole state, the controller's included, bit for bit. The suite checks it on
    /// .NET and in Unity's EditMode (Mono), tools/flightbench --golden on .NET Release. A re-record raises
    /// LogFormat.ModelVersion and is audited case by case (CLAUDE.md).
    /// </summary>
    public static class GoldenFlights
    {
        public const int PoweredSteps = 10000;

        // 1.4: first recorded with LogFormat.ModelVersion 2.
        public const ulong PoweredHash = 0xED68941C901FBA75UL;

        public static FlightSetup PoweredSetup(DroneSpec spec) => new FlightSetup
        {
            Seed = 0x450F,
            World = FlightWorld.Floor(),
            Mass = spec.BuildMass(spec.Nominal),
            Propulsion = spec.BuildPropulsion(),
            StartPosition = new Double3(0, 300, 0),
            StartPackCharge = 0.9,
        };

        /// <summary>The scripted pilot's sticks at step k.</summary>
        public static StickSample PoweredStick(int k)
        {
            double t = k * FlightCore.Dt;
            var s = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 20 };
            if (k < 1000) s.Throttle = 0.47 * DetMath.Max(0, t - 0.1) / 0.9;
            else s.Throttle = 0.47;
            if (k >= 2000 && k < 2500) s.Roll = 0.9575;
            if (k >= 2500 && k < 3000) s.Roll = -0.5;
            if (k >= 3000 && k < 3500) s.Pitch = 0.6;
            if (k >= 3500 && k < 4500) s.Yaw = 1;
            if (k >= 4500 && k < 5500) s.Throttle = 1;
            if (k >= 5500 && k < 6000) s.Throttle = 0;
            if (k >= 6000)
            {
                double phase = (k - 6000) % 1000 / 1000.0;
                s.Throttle = 0.5;
                s.Roll = phase < 0.5 ? 0.4 : -0.4;
                s.Pitch = phase < 0.25 || phase >= 0.75 ? -0.2 : 0.2;
            }
            return s;
        }

        /// <summary>Flies the powered golden flight; returns its hash, with the core's last state for a report.</summary>
        public static ulong FlyPowered(DroneSpec spec, out FlightCore core)
        {
            core = new FlightCore(PoweredSetup(spec));
            var record = new byte[LogFormat.StepSize];
            var e = new byte[LogFormat.EventSize];
            ulong hash = 0xCBF29CE484222325UL;
            for (int k = 0; k < PoweredSteps; k++)
            {
                StickSample stick = PoweredStick(k);
                core.Advance(stick);
                LogFormat.EncodeStep(record, core.State, stick, core.Trace);
                hash = Fnv(hash, record);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    LogFormat.EncodeEvent(e, core.State.Step, core.Events[i]);
                    hash = Fnv(hash, e);
                }
            }
            return hash;
        }

        static ulong Fnv(ulong h, byte[] bytes)
        {
            foreach (byte b in bytes) h = unchecked((h ^ b) * 0x100000001B3UL);
            return h;
        }
    }
}
