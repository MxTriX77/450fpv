using System;
using System.Diagnostics;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.FlightBench
{
    // The world-query share of the step (task 1.7, PLT-04): the world passed through, with the timer's ticks inside the
    // step's queries (SampleGround, StaticContacts, Raycast, Geometry) and their count. Bench only: its own clock reads
    // cost tens of ns a call, so the cases time the step without it and measure the share in a pass of their own.
    internal sealed class TimedQuery : IWorldQuery
    {
        readonly IWorldQuery _world;
        public long Ticks, Calls;

        public TimedQuery(IWorldQuery world) => _world = world;

        public ulong ContentHash => _world.ContentHash;
        public int QueryVersion => _world.QueryVersion;

        public void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results)
        {
            long t = Stopwatch.GetTimestamp();
            _world.SampleGround(points, results);
            Ticks += Stopwatch.GetTimestamp() - t;
            Calls++;
        }

        public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results)
        {
            long t = Stopwatch.GetTimestamp();
            int found = _world.StaticContacts(previous, current, margin, results);
            Ticks += Stopwatch.GetTimestamp() - t;
            Calls++;
            return found;
        }

        public void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits)
        {
            long t = Stopwatch.GetTimestamp();
            _world.Raycast(rays, maxDistance, hits);
            Ticks += Stopwatch.GetTimestamp() - t;
            Calls++;
        }

        public bool Geometry(int obj, int shape, out ShapeGeometry geometry)
        {
            long t = Stopwatch.GetTimestamp();
            bool found = _world.Geometry(obj, shape, out geometry);
            Ticks += Stopwatch.GetTimestamp() - t;
            Calls++;
            return found;
        }

        public int AddObject(string name, ReadOnlySpan<ShapeGeometry> shapes) => _world.AddObject(name, shapes);
        public void ResetRuntimeObjects() => _world.ResetRuntimeObjects();
        public string ObjectName(int obj) => _world.ObjectName(obj);
        public string MaterialName(ushort material) => _world.MaterialName(material);
        public ushort MaterialId(string name) => _world.MaterialId(name);
        public string SurfaceName(byte surface) => _world.SurfaceName(surface);

        /// <summary>
        /// Flies a fresh setup (its world wrapped) warmup steps, then steps more, and gives the queries' share of those
        /// steps' time, their calls a step, and their time in a step at p99 and at most (PLT-04: under 60 µs).
        /// </summary>
        public static string Share(FlightSetup setup, Func<FlightCore, int, StickSample> stick, int warmup, int steps)
        {
            var timed = new TimedQuery(setup.World.Query);
            setup.World = new FlightWorld(timed);
            var core = new FlightCore(setup);
            for (int k = 0; k < warmup; k++) core.Advance(stick(core, k));
            timed.Ticks = timed.Calls = 0;
            var per = new long[steps];
            long t0 = Stopwatch.GetTimestamp();
            for (int k = warmup; k < warmup + steps; k++)
            {
                long before = timed.Ticks;
                core.Advance(stick(core, k));
                per[k - warmup] = timed.Ticks - before;
            }
            long total = Stopwatch.GetTimestamp() - t0;
            Array.Sort(per);
            double us = 1e6 / Stopwatch.Frequency;
            return $"world queries {100.0 * timed.Ticks / total:F1} % of the step ({(double)timed.Calls / steps:F1} calls a step; "
                + $"p99 {per[steps * 99 / 100] * us:F1} µs, max {per[steps - 1] * us:F1} µs a step)";
        }
    }
}
