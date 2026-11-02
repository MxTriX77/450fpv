using System;
using System.Collections.Generic;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Tests
{
    // A minimal flight record for the scheduler's tests (the log is Log.FlightRecorder): one FNV-1a 64 hash of every
    // step's state and stick bits, and the hitches. Preallocated, so recording allocates nothing.
    public sealed class StateDump : IFlightRecorder
    {
        readonly ulong[] _steps;
        public int Count;
        public readonly List<Hitch> Hitches = new List<Hitch>(16);

        public StateDump(int capacity) => _steps = new ulong[capacity];

        public ulong this[int i] => _steps[i];

        public void RecordStep(in FlightState state, in StickSample stick, in StepTrace trace, StepEvents events)
        {
            if (Count < _steps.Length) _steps[Count] = Hash(state, stick);
            Count++;
        }

        public void RecordHitch(in Hitch hitch) => Hitches.Add(hitch);

        // The first step where two dumps differ, or −1.
        public static int FirstDifference(StateDump a, StateDump b)
        {
            int n = Math.Min(Math.Min(a.Count, b.Count), a._steps.Length);
            for (int i = 0; i < n; i++)
                if (a._steps[i] != b._steps[i])
                    return i;
            return a.Count == b.Count ? -1 : n;
        }

        public static ulong Hash(in FlightState s, in StickSample k)
        {
            ulong h = 0xCBF29CE484222325UL;
            Mix(ref h, (ulong)s.Step);
            Mix(ref h, s.Position);
            Mix(ref h, s.Velocity);
            Mix(ref h, s.Attitude.X);
            Mix(ref h, s.Attitude.Y);
            Mix(ref h, s.Attitude.Z);
            Mix(ref h, s.Attitude.W);
            Mix(ref h, s.BodyRate);
            Mix(ref h, s.Armed ? 1UL : 0UL);
            for (int i = 0; i < 4; i++)
            {
                Mix(ref h, s.RotorSpeed[i]);
                Mix(ref h, s.EscCommand[i]);
            }
            Mix(ref h, s.PackCharge0);
            Mix(ref h, s.PackCharge1);
            Mix(ref h, s.EscLimited);
            Mix(ref h, (ulong)k.HostUs);
            Mix(ref h, k.Roll);
            Mix(ref h, k.Pitch);
            Mix(ref h, k.Yaw);
            Mix(ref h, k.Throttle);
            Mix(ref h, k.Arm ? 1UL : 0UL);
            return h;
        }

        static void Mix(ref ulong h, Double3 v)
        {
            Mix(ref h, v.X);
            Mix(ref h, v.Y);
            Mix(ref h, v.Z);
        }

        static void Mix(ref ulong h, double v) => Mix(ref h, (ulong)BitConverter.DoubleToInt64Bits(v));

        static void Mix(ref ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
                h = unchecked((h ^ ((v >> (8 * i)) & 0xFF)) * 0x100000001B3UL);
        }
    }
}
