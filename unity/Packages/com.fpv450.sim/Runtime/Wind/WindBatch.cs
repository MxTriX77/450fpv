// WindBatch: the wind at a batch of points evaluated on a worker thread alongside the flight thread (p7d-tether 1.13).
// - The field's At is a pure function of the step's frame (WindField.Begin) and the point: its plane waves, the wake grid's
//   sample, the canopy's and the cut's flow only read. So a point's wind is the same bits on any thread, and evaluating
//   the line's nodes on a worker while the flight thread steps the drone changes no result, only when it is computed.
// - Post (the flight thread, after Begin): the points, then one 64-bit claim word (the batch's generation, its count and
//   the next index) published. The worker, woken, and the flight thread at Join both take points by compare-and-swap on
//   the claim word, so each point is evaluated exactly once by whichever thread gets it, and the flight thread never
//   waits for a worker that has not woken: at worst it evaluates every point itself, as without the batch.
// - Join (the flight thread, before it reads the winds and before the next Begin): takes what is left, then waits for the
//   points the worker holds (one at most). After it nothing runs on the worker until the next Post, so Begin never
//   changes the frame under a running evaluation and a batch never outlives its step.
// - Take hands the winds back by position: a node whose position is the posted point's, bit for bit, takes its wind
//   (equal inputs, equal output); any other (a node the contacts put in after Post) is evaluated by the caller.
// - Allocation-free after the constructor, on both threads. The worker sleeps on an event between batches.

using System;
using System.Threading;

namespace Fpv450.Sim.Wind
{
    public sealed class WindBatch : IDisposable
    {
        const int Bits = 20;
        const long Mask = (1L << Bits) - 1;

        readonly WindField _field;
        readonly Double3[] _points, _winds;
        readonly Thread _thread;
        readonly AutoResetEvent _wake = new AutoResetEvent(false);
        volatile bool _stop;
        long _claim;     // generation << 40 | count << 20 | next index
        int _finished;   // points of this generation evaluated
        long _generation;
        int _count, _cursor;
        bool _joined = true;

        /// <summary>The points a batch holds at most.</summary>
        public int Capacity => _points.Length;

        /// <summary>Points the worker evaluated over the flight.</summary>
        public long ByWorker => Interlocked.Read(ref _byWorker);

        /// <summary>Points the flight thread evaluated at Join over the flight (the worker had not taken them).</summary>
        public long ByCaller { get; private set; }

        long _byWorker;

        public WindBatch(WindField field, int capacity)
        {
            if (capacity < 1 || capacity > Mask) throw new ArgumentOutOfRangeException(nameof(capacity));
            _field = field;
            _points = new Double3[capacity];
            _winds = new Double3[capacity];
            // The flight thread's priority: the worker's points are on the step's path.
            _thread = new Thread(Run) { IsBackground = true, Name = "fpv450 wind batch", Priority = ThreadPriority.Highest };
            _thread.Start();
        }

        /// <summary>Starts a batch (after Join): no points.</summary>
        public void Clear()
        {
            Join();
            _count = 0;
        }

        /// <summary>Adds a point to the batch being built; false when it is full (the point is then the caller's).</summary>
        public bool Add(in Double3 point)
        {
            if (_count == _points.Length) return false;
            _points[_count++] = point;
            return true;
        }

        /// <summary>Publishes the batch to the worker (the step's frame begun).</summary>
        public void Post()
        {
            if (_count == 0) return;
            _joined = false;
            Volatile.Write(ref _finished, 0);
            _generation++;
            Volatile.Write(ref _claim, ((_generation & 0x7FFFFF) << 40) | ((long)_count << Bits));
            _wake.Set();
        }

        /// <summary>Evaluates what the worker has not taken and waits for what it holds; every wind is then ready.</summary>
        public void Join()
        {
            _cursor = 0;
            if (_joined) return;
            ByCaller += Drain();
            // The worker's last point (a few µs): spin, yielding now and then in case it shares this core; never Sleep(1).
            for (int k = 1; Volatile.Read(ref _finished) < _count; k++)
            {
                if ((k & 63) == 0) Thread.Yield();
                else Thread.SpinWait(20);
            }
            _joined = true;
        }

        /// <summary>
        /// After Join, in the posted order: the wind at `point` if it is the next posted point bit for bit (then the cursor
        /// moves on); false otherwise, the caller evaluating it.
        /// </summary>
        public bool Take(in Double3 point, out Double3 wind)
        {
            if (_joined && _cursor < _count && Same(_points[_cursor], point))
            {
                wind = _winds[_cursor++];
                return true;
            }
            wind = default;
            return false;
        }

        static bool Same(in Double3 a, in Double3 b) =>
            BitConverter.DoubleToInt64Bits(a.X) == BitConverter.DoubleToInt64Bits(b.X)
            && BitConverter.DoubleToInt64Bits(a.Y) == BitConverter.DoubleToInt64Bits(b.Y)
            && BitConverter.DoubleToInt64Bits(a.Z) == BitConverter.DoubleToInt64Bits(b.Z);

        // Takes points of the current generation until none is left; the count taken.
        int Drain()
        {
            int taken = 0;
            while (true)
            {
                long c = Volatile.Read(ref _claim);
                int i = (int)(c & Mask), n = (int)((c >> Bits) & Mask);
                if (i >= n) return taken;
                if (Interlocked.CompareExchange(ref _claim, c + 1, c) != c) continue;
                _winds[i] = _field.At(_points[i]);
                Interlocked.Increment(ref _finished);
                taken++;
            }
        }

        void Run()
        {
            while (true)
            {
                _wake.WaitOne();
                if (_stop) return;
                int taken = Drain();
                if (taken > 0) Interlocked.Add(ref _byWorker, taken);
            }
        }

        public void Dispose()
        {
            if (_stop) return;
            _stop = true;
            _wake.Set();
            _thread.Join();
            _wake.Dispose();
        }
    }
}
