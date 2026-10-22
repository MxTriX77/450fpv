using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The powered golden flight (p6-flight-core 1.4, 1.5): the repository's drone at its nominal loadout with its drag,
    /// in the Windy stand-in wind, armed by Betaflight's rules and flown 10 s by a scripted pilot through every part
    /// of the controller, propulsion and the air (a spin-up, a climb, roll, pitch and yaw steps, a throttle punch into
    /// the ESCs' current limit and a chop, a weave). Its hash is every step's log
    /// record and events, so it pins the whole state, the controller's included, bit for bit. The suite checks it on
    /// .NET and in Unity's EditMode (Mono), tools/flightbench --golden on .NET Release. A re-record raises
    /// LogFormat.ModelVersion and is audited case by case (CLAUDE.md).
    /// </summary>
    public static class GoldenFlights
    {
        public const int PoweredSteps = 10000;

        // 1.4: first recorded with LogFormat.ModelVersion 2: 0xED68941C901FBA75.
        // 1.5: re-recorded with ModelVersion 3 (the 992-byte record), audited case by case against model 2's flight on
        // the records' shared 912 bytes: with the air off (no drag, still air, no ESC limit) and the gyroscopic term
        // taken out, every record of all 10 000 steps is bit-identical; then each new term moves the flight from its
        // first step of effect: the gyroscopic moment from step 1133 (0.17 m by 10 s), the rotors' in-plane drag from
        // step 22 (model 2's drone fell 315 m with no drag at all; it now ends 219 m up), the 40 A limit from step
        // 2006 (the roll step's kick), the body's and the coil's drag from step 2, the wind from step 1.
        public const ulong PoweredHash = 0x3A5D9ED8AECA878CUL;

        public static FlightSetup PoweredSetup(DroneSpec spec) => new FlightSetup
        {
            Seed = 0x450F,
            World = FlightWorld.Floor(),
            Mass = spec.BuildMass(spec.Nominal),
            Propulsion = spec.BuildPropulsion(),
            Aero = spec.BuildAero(),
            Wind = WindSetup.Of(WindLevel.Windy),
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
