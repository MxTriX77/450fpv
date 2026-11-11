using System;
using System.Diagnostics;

namespace Fpv450.LagProbe
{
    // A step's phases, in the order the timed copies mark them (timed_sources.py). Start is Advance's head before the
    // first mark; Tether is FlightCore's share of the line around FiberLine.Step (and LinkField); the Tether* phases
    // are FiberLine.Step's own.
    internal enum Phase
    {
        Start, Wind, Flight, ContactForces, Body, Loose, ContactSense, Strikes, Tether, TetherPrepare, TetherGround,
        TetherRotors, TetherLoads, TetherAssign, TetherSolve, TetherFinish, TetherCut, TetherRest, WindTrace, Rain, Count,
    }

    // The world door's calls (FlightWorld), timed; Soil is SoilUnder's own ray; Shape is one shape's contact (ShapeContact).
    internal enum Query { Ground, Static, Raycast, Micro, Snags, Wires, Geometry, Soil, Shape, Count }

    // The clock marks the timed copies call. Marks partition the flight thread's step into phases; the world's calls
    // made on that thread add their time to the phase they fall in. Calls on other threads (the wind's worker) are
    // not counted: the flight thread meets them only where it waits (Wind, WindTrace).
    internal static class Probe
    {
        public static readonly long[] Ticks = new long[(int)Phase.Count];
        public static readonly long[] QueryTicks = new long[(int)Phase.Count];
        public static readonly long[] QueryCalls = new long[(int)Phase.Count];
        public static readonly long[] KindTicks = new long[(int)Query.Count];
        public static readonly long[] KindCalls = new long[(int)Query.Count];

        static int _owner = -1;
        static Phase _phase;
        static long _last;

        public static readonly double Us = 1e6 / Stopwatch.Frequency;

        public static void StepBegin()
        {
            _owner = Environment.CurrentManagedThreadId;
            _phase = Phase.Start;
            _last = Stopwatch.GetTimestamp();
        }

        public static void StepEnd() => Mark(Phase.Start);

        public static void Mark(Phase next)
        {
            if (Environment.CurrentManagedThreadId != _owner) return;
            long now = Stopwatch.GetTimestamp();
            Ticks[(int)_phase] += now - _last;
            _last = now;
            _phase = next;
        }

        public static long Q0() => Environment.CurrentManagedThreadId == _owner ? Stopwatch.GetTimestamp() : 0;

        public static void Q1(long start, Query kind)
        {
            if (start == 0) return;
            long t = Stopwatch.GetTimestamp() - start;
            QueryTicks[(int)_phase] += t;
            QueryCalls[(int)_phase]++;
            KindTicks[(int)kind] += t;
            KindCalls[(int)kind]++;
        }
    }
}
