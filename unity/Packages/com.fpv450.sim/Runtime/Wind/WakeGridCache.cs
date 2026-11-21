// WakeGridCache: a flight's first wake grid, kept for the next flight over the same obstacles (p6-flight-core, the
// build's freeze). A new flight's WindField took 0.2–0.9 s on the test range to build its wake grid for the drawn
// bearing (WakeGrid.Build), the frame's freeze on every R, Fly and restart; WakeGrid.Build now spreads it over the cores,
// and this keeps the last build:
// - The key is the layout's content, bit for bit (its origin, its size, its version and every volume's fields), and the bearing's
//   bits; the grid is a pure function of them (WakeGrid's header, WakeGrid.Version within the process). A flight with
//   the same layout and bearing (a restart with the same seed, a replay, a test's second flight) gets a copy of the
//   kept grid, bit for bit what a build gives; the same layout at another bearing shares the obstacle grid and builds.
// - The kept grid is never handed out: each flight's worker owns its copy and rebuilds it in place as the wind turns.
// - Load time only: the FlightCore constructor (through WindField's) calls it, never a step. Its memory is one obstacle
//   grid and one wake grid (since WakeGrid version 2 each cell holds two wakes: on the test range's 256 × 256 cells about
//   17 MB, on the farmland's 500 × 500 about 64 MB), held for the process.

using System;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Wind
{
    public static class WakeGridCache
    {
        sealed class Entry
        {
            public double OriginX, OriginZ;
            public int NX, NZ, Version;
            public WindVolume[] Volumes; // the layout's, copied
            public WakeObstacles Obstacles;
            public WakeGrid Grid;        // the kept build, never handed out
        }

        static readonly object Lock = new object();
        static Entry _last;

        /// <summary>Workers started from a copy of the kept grid, and from a build (for tests and the bench).</summary>
        public static int Hits { get; private set; }
        public static int Misses { get; private set; }

        /// <summary>The wake grid's worker of a flight over `layout` (checked) with wind from `fromDeg` (degrees) at its start:
        /// its first grid a copy of the kept one for the same layout and bearing, else built and kept. Load time: allocates.</summary>
        public static WakeGridWorker Worker(WakeLayout layout, double fromDeg)
        {
            lock (Lock)
            {
                Entry last = _last;
                bool same = last != null && Same(last, layout);
                if (same && Bits(last.Grid.FromDeg) == Bits(fromDeg))
                {
                    Hits++;
                    return new WakeGridWorker(last.Grid.Copy());
                }
                Misses++;
                WakeObstacles obstacles = same ? last.Obstacles : layout.Obstacles();
                WakeGrid grid = WakeGrid.Build(obstacles, fromDeg);
                _last = new Entry
                {
                    OriginX = layout.OriginX,
                    OriginZ = layout.OriginZ,
                    NX = layout.NX,
                    NZ = layout.NZ,
                    Version = layout.Version,
                    Volumes = (WindVolume[])layout.Volumes.Clone(),
                    Obstacles = obstacles,
                    Grid = grid.Copy(),
                };
                return new WakeGridWorker(grid);
            }
        }

        /// <summary>Forgets the kept grid (for tests and the bench).</summary>
        public static void Clear()
        {
            lock (Lock) _last = null;
        }

        static bool Same(Entry e, WakeLayout layout)
        {
            if (Bits(e.OriginX) != Bits(layout.OriginX) || Bits(e.OriginZ) != Bits(layout.OriginZ) || e.NX != layout.NX || e.NZ != layout.NZ
                || e.Version != layout.Version || e.Volumes.Length != layout.Volumes.Length)
                return false;
            for (int i = 0; i < e.Volumes.Length; i++)
            {
                WindVolume a = e.Volumes[i], b = layout.Volumes[i];
                if (a.Kind != b.Kind || Bits(a.X) != Bits(b.X) || Bits(a.Z) != Bits(b.Z) || Bits(a.BaseM) != Bits(b.BaseM) || Bits(a.TopM) != Bits(b.TopM)
                    || Bits(a.HalfX) != Bits(b.HalfX) || Bits(a.HalfZ) != Bits(b.HalfZ) || Bits(a.Radius) != Bits(b.Radius) || Bits(a.Yaw) != Bits(b.Yaw)
                    || Bits(a.Porosity) != Bits(b.Porosity))
                    return false;
            }
            return true;
        }

        static long Bits(double v) => BitConverter.DoubleToInt64Bits(v);
    }
}
