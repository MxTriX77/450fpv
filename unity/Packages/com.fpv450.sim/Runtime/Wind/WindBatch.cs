// WindBatch: the wind at a batch of points evaluated on a worker thread alongside the flight thread (p7d-tether 1.13).
// - The field's At is a pure function of the step's frame (WindField.Begin) and the point: its plane waves, the wake grid's
//   sample, the canopy's and the cut's flow only read. So a point's wind is the same bits on any thread, and evaluating
//   points on a worker while the flight thread steps the drone changes no result, only when it is computed.
// - Post (the flight thread, after Begin): the points, then one 64-bit claim word (the batch's generation, its count and
//   the next index) published. The worker, woken, and the flight thread both take points in order by compare-and-swap on
//   the claim word, so each point is evaluated exactly once by whichever thread gets it. The flight thread asking for a
//   point (At, TryAt) first takes every point up to it the worker has not, then waits only for those the worker holds (one
//   at most): it never waits for a worker that has not woken, at worst evaluating every point itself, as without the batch.
// - Join (the flight thread, before the next Begin): takes what is left and waits for what the worker holds. After it
//   nothing runs on the worker until the next Post, so Begin never changes the frame under a running evaluation and a
//   batch never outlives its step.
// - A point is handed back only to a caller asking at its own position, bit for bit (equal inputs, equal output); any
//   other is evaluated by the caller.
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
        readonly int[] _done;   // the generation stamp a point was evaluated in
        readonly Thread _thread;
        readonly AutoResetEvent _wake = new AutoResetEvent(false);
        volatile bool _stop;
        long _claim;            // generation << 40 | count << 20 | next index
        long _generation;
        int _stamp, _count;
        bool _joined = true;
        long _byWorker;

        /// <summary>The points a batch holds at most.</summary>
        public int Capacity => _points.Length;

        /// <summary>Points the worker evaluated over the flight.</summary>
        public long ByWorker => Interlocked.Read(ref _byWorker);

        /// <summary>Points the flight thread took from the batch over the flight (the worker had not taken them).</summary>
        public long ByCaller { get; private set; }

        public WindBatch(WindField field, int capacity)
        {
            if (capacity < 1 || capacity > Mask) throw new ArgumentOutOfRangeException(nameof(capacity));
            _field = field;
            _points = new Double3[capacity];
            _winds = new Double3[capacity];
            _done = new int[capacity];
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

        /// <summary>Adds a point to the batch being built: its index, or −1 when the batch is full (the point is the caller's).</summary>
        public int Add(in Double3 point)
        {
            if (_count == _points.Length) return -1;
            _points[_count] = point;
            return _count++;
        }

        /// <summary>Publishes the batch to the worker (the step's frame begun).</summary>
        public void Post()
        {
            if (_count == 0) return;
            _joined = false;
            _generation++;
            _stamp = (int)(_generation & 0x7FFFFFFF);
            if (_stamp == 0) _stamp = 1; // 0 is no generation's: the stamps start at 0
            Volatile.Write(ref _claim, ((_generation & 0x7FFFFF) << 40) | ((long)_count << Bits));
            _wake.Set();
        }

        /// <summary>Evaluates what the worker has not taken and waits for what it holds; every wind is then ready.</summary>
        public void Join()
        {
            if (_joined) return;
            Ready(_count);
            _joined = true;
        }

        /// <summary>The wind at posted point `index` if `point` is that point bit for bit (it is then ready), else false.</summary>
        public bool TryAt(int index, in Double3 point, out Double3 wind)
        {
            if (_joined || index < 0 || index >= _count || !Same(_points[index], point))
            {
                wind = default;
                return false;
            }
            Ready(index + 1);
            wind = _winds[index];
            return true;
        }

        /// <summary>The wind at `point`: posted point `index`'s if it is that point bit for bit, else the field's own.</summary>
        public Double3 At(int index, in Double3 point) => TryAt(index, point, out Double3 wind) ? wind : _field.At(point);

        static bool Same(in Double3 a, in Double3 b) =>
            BitConverter.DoubleToInt64Bits(a.X) == BitConverter.DoubleToInt64Bits(b.X)
            && BitConverter.DoubleToInt64Bits(a.Y) == BitConverter.DoubleToInt64Bits(b.Y)
            && BitConverter.DoubleToInt64Bits(a.Z) == BitConverter.DoubleToInt64Bits(b.Z);

        // Every point below `end` evaluated: those the worker has not taken here, then a wait for those it holds (a few µs:
        // spinning, yielding now and then in case it shares this core; never Sleep(1)).
        void Ready(int end)
        {
            ByCaller += Take(end);
            for (int i = 0; i < end; i++)
                for (int k = 1; Volatile.Read(ref _done[i]) != _stamp; k++)
                {
                    if ((k & 63) == 0) Thread.Yield();
                    else Thread.SpinWait(20);
                }
        }

        // Takes points of the current generation in order while the next is below `end`; the count taken.
        int Take(int end)
        {
            int taken = 0;
            while (true)
            {
                long c = Volatile.Read(ref _claim);
                int i = (int)(c & Mask), n = (int)((c >> Bits) & Mask);
                if (i >= n || i >= end) return taken;
                if (Interlocked.CompareExchange(ref _claim, c + 1, c) != c) continue;
                // The claim names this generation (it changes only at a Post, after Join), so _stamp is this generation's.
                int stamp = Volatile.Read(ref _stamp);
                _winds[i] = _field.At(_points[i]);
                Volatile.Write(ref _done[i], stamp);
                taken++;
            }
        }

        void Run()
        {
            while (true)
            {
                _wake.WaitOne();
                if (_stop) return;
                int taken = Take(int.MaxValue);
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
