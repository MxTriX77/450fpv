// WakeGridWorker: the wake grid's worker thread and its deterministic handover (p7c-wind 1.3, design 4, K§6.7).
// - The first grid is built before the flight, on the caller's thread (the constructor, or WakeGridCache's copy of a
//   build for the same obstacles and direction); the spare is only allocated, since the worker rebuilds it before
//   anything reads it.
// - Step (the flight thread, every step): once the wind's direction is more than 5° from the active grid's (K§6.7),
//   it asks the worker for a grid at that direction and notes the step. The new grid takes over LatencySteps later,
//   and fades in over FadeSteps (Sample blends the two). Both steps are fixed counts from the request, never "when the
//   worker is done", so a replay switches grids at the same step with the same bits (the grid is a pure function of
//   the obstacles and the direction). Should the worker not be done by then, Step waits for it and counts a stall:
//   that costs real time, never determinism. One request at a time; the next one is judged after the fade.
// - The worker rebuilds a spare grid in place (WakeGrid.Rebuild): after the constructor nothing is allocated, on either
//   thread, so no garbage collection can pause the flight thread.
// - The rebuild is shared by the worker and its helpers (w9 step budget): each fills whole rows of cells, claimed one at a
//   time, with its own lane (WakeGrid.BeginRebuild, FillRow), so the grid's bits are a one-thread rebuild's whatever the
//   threads and their order. The farmland's grid (500 × 500 cells) took one thread past LatencySteps at 6 of the 10 takeovers
//   in the pilot's model 100 flights (Mono, the laptop's Quiet mode; up to 4.4 s), and the flight thread waited for it there:
//   stalls of 76 ms to 1.4 s.
// - Step and Sample allocate nothing and belong to one thread (the flight thread); Sample is reentrant on it.
// The window that would follow the drone over a large map (K§6.7, "or the window moves") is not built yet: the grid
// covers its whole obstacle grid (task 1.3's handoff).

using System;
using System.Diagnostics;
using System.Threading;

namespace Fpv450.Sim.World
{
    public sealed class WakeGridWorker : IDisposable
    {
        /// <summary>The direction change that asks for a new grid, degrees (K§6.7).</summary>
        public const double RecomputeDeg = 5;

        /// <summary>Steps from a request to the new grid taking over (assumed: 3 s at 1 kHz, several times the build's
        /// time on the dev machine, worldbench --wakes).</summary>
        public const long LatencySteps = 3000;

        /// <summary>Steps of the cross-fade from the old grid to the new one (assumed: 2 s, so the wakes move smoothly).</summary>
        public const long FadeSteps = 2000;

        /// <summary>
        /// The helpers a worker starts for its rebuilds: a quarter of the host's logical processors, at most 3 (assumed: the
        /// flight thread, the wind batch's worker and the engine's own threads keep theirs; the pilot's laptop's 16 give the
        /// worker 3 helpers, a 4-thread build).
        /// </summary>
        public static readonly int DefaultHelpers = Math.Min(3, Environment.ProcessorCount / 4);

        readonly WakeGrid[] _grids = new WakeGrid[2];
        readonly Thread _thread;
        readonly AutoResetEvent _request = new AutoResetEvent(false);
        volatile bool _stop, _built;
        double _requestDeg;
        long _lastBuildTicks;

        // The helpers: their threads and wake-ups, the lanes (the worker's last), the rebuild being shared (its grid and march
        // steps), the next row to claim, the threads still filling (the worker counted) and the last one's signal.
        readonly Thread[] _helpers;
        readonly AutoResetEvent[] _go;
        readonly WakeObstacles.Lane[] _lanes;
        readonly AutoResetEvent _done = new AutoResetEvent(false);
        WakeGrid _building;
        int _buildSteps, _nextRow, _filling, _generation;

        int _active = 0;            // the index of the active grid
        bool _fading;               // the other grid is fading in
        long _requestStep = -1;     // the step of the pending request, or −1
        long _fadeStart;
        double _alpha;              // the fading grid's weight at this step

        /// <summary>The grid the step samples (during a fade, the one fading out).</summary>
        public WakeGrid Active => _grids[_active];

        /// <summary>The grid fading in, or null.</summary>
        public WakeGrid Fading => _fading ? _grids[1 - _active] : null;

        /// <summary>The fading grid's weight at this step (0 outside a fade).</summary>
        public double FadeWeight => _alpha;

        /// <summary>Whether a request is pending (asked, not yet taken over).</summary>
        public bool Pending => _requestStep >= 0 && !_fading;

        /// <summary>The steps at which Step had to wait for the worker: 0 unless the worker is too slow.</summary>
        public int Stalls { get; private set; }

        /// <summary>How many grids the worker has built.</summary>
        public int Builds { get; private set; }

        /// <summary>The last build's time on the worker, s.</summary>
        public double LastBuildSeconds => Interlocked.Read(ref _lastBuildTicks) / (double)Stopwatch.Frequency;

        /// <summary>Builds the first grid (for wind from `fromDeg`, degrees) and starts the worker.</summary>
        public WakeGridWorker(WakeObstacles obstacles, double fromDeg) : this(WakeGrid.Build(obstacles, fromDeg)) { }

        /// <summary>Starts the worker with `first` as the active grid (built for its FromDeg) and DefaultHelpers helpers; the
        /// worker owns it from now.</summary>
        public WakeGridWorker(WakeGrid first) : this(first, DefaultHelpers) { }

        /// <summary>The same with `helpers` helper threads (0: the worker rebuilds alone).</summary>
        public WakeGridWorker(WakeGrid first, int helpers)
        {
            _grids[0] = first ?? throw new ArgumentNullException(nameof(first));
            _grids[1] = WakeGrid.Spare(first.Obstacles);
            if (helpers < 0) throw new ArgumentOutOfRangeException(nameof(helpers));
            _helpers = new Thread[helpers];
            _go = new AutoResetEvent[helpers];
            _lanes = new WakeObstacles.Lane[helpers + 1];
            for (int i = 0; i <= helpers; i++) _lanes[i] = first.Obstacles.NewLane();
            // Below normal: the precompute yields to the flight thread whenever they share a core.
            for (int i = 0; i < helpers; i++)
            {
                int index = i;
                _go[i] = new AutoResetEvent(false);
                _helpers[i] = new Thread(() => Help(index)) { IsBackground = true, Name = "wake grid helper", Priority = ThreadPriority.BelowNormal };
                _helpers[i].Start();
            }
            _thread = new Thread(Run) { IsBackground = true, Name = "wake grid", Priority = ThreadPriority.BelowNormal };
            _thread.Start();
        }

        void Run()
        {
            while (true)
            {
                _request.WaitOne();
                if (_stop) return;
                double deg = Volatile.Read(ref _requestDeg);
                long t0 = Stopwatch.GetTimestamp();
                WakeGrid grid = _grids[1 - Volatile.Read(ref _active)];
                _buildSteps = grid.BeginRebuild(deg);
                _building = grid;
                _nextRow = 0;
                _filling = _helpers.Length + 1;
                Volatile.Write(ref _generation, _generation + 1);
                for (int i = 0; i < _go.Length; i++) _go[i].Set(); // the event's set and wait order the writes above before the helper's reads
                FillRows(_lanes[_helpers.Length]);
                if (Interlocked.Decrement(ref _filling) != 0) _done.WaitOne(); // the last helper's signal: every row is filled
                Interlocked.Exchange(ref _lastBuildTicks, Stopwatch.GetTimestamp() - t0);
                _built = true;
            }
        }

        // A helper: on each wake-up for a new rebuild, rows of it until none is left (the last thread done signals the worker);
        // a stop it wakes to after its rebuild's (Dispose's signal may merge with the rebuild's) ends it.
        void Help(int index)
        {
            int seen = 0;
            while (true)
            {
                _go[index].WaitOne();
                int generation = Volatile.Read(ref _generation);
                if (generation != seen)
                {
                    seen = generation;
                    FillRows(_lanes[index]);
                    if (Interlocked.Decrement(ref _filling) == 0) _done.Set();
                }
                if (_stop) return;
            }
        }

        // Rows of the shared rebuild, claimed one at a time, until every row is taken.
        void FillRows(WakeObstacles.Lane lane)
        {
            WakeGrid grid = _building;
            int steps = _buildSteps, rows = grid.Obstacles.NZ;
            for (int j = Interlocked.Increment(ref _nextRow) - 1; j < rows; j = Interlocked.Increment(ref _nextRow) - 1)
                grid.FillRow(j, steps, lane);
        }

        /// <summary>
        /// The flight thread's call at every step, with the wind's direction (degrees, as WakeGrid.FromDeg) at that step:
        /// requests, takes over and fades as the header says. Allocation-free.
        /// </summary>
        public void Step(long step, double fromDeg)
        {
            if (_fading)
            {
                long t = step - _fadeStart;
                if (t < FadeSteps)
                {
                    _alpha = t / (double)FadeSteps;
                    return;
                }
                Volatile.Write(ref _active, 1 - _active);
                _fading = false;
                _alpha = 0;
                _requestStep = -1;
            }
            if (_requestStep >= 0)
            {
                if (step - _requestStep < LatencySteps) return;
                if (!_built)
                {
                    // A stall: spin, then yield and sleep (SpinWait), allocating nothing.
                    Stalls++;
                    var spin = new SpinWait();
                    while (!_built) spin.SpinOnce();
                }
                Builds++;
                _fading = true;
                _fadeStart = step;
                _alpha = 0;
                return;
            }
            if (Degrees(fromDeg, Active.FromDeg) > RecomputeDeg)
            {
                Volatile.Write(ref _requestDeg, fromDeg);
                _built = false;
                _requestStep = step;
                _request.Set();
            }
        }

        /// <summary>The wake at world (x, z) and a height above the ground (m), blended across a fade. Allocation-free.</summary>
        public void Sample(double x, double z, double heightAboveGround, out WakeSample sample)
        {
            Active.Sample(x, z, heightAboveGround, out sample);
            if (!_fading) return;
            _grids[1 - _active].Sample(x, z, heightAboveGround, out WakeSample next);
            double a = _alpha;
            sample.Speed += a * (next.Speed - sample.Speed);
            sample.Turbulence += a * (next.Turbulence - sample.Turbulence);
            if (next.ObstacleHeight > sample.ObstacleHeight) sample.ObstacleHeight = next.ObstacleHeight;
        }

        /// <summary>The angle between two bearings, degrees, in [0, 180].</summary>
        public static double Degrees(double a, double b)
        {
            double d = a - b;
            while (d > 180) d -= 360;
            while (d <= -180) d += 360;
            return DetMath.Abs(d);
        }

        public void Dispose()
        {
            _stop = true;
            _request.Set();
            _thread.Join();
            for (int i = 0; i < _helpers.Length; i++)
            {
                _go[i].Set();
                _helpers[i].Join();
                _go[i].Dispose();
            }
            _request.Dispose();
            _done.Dispose();
        }
    }
}
