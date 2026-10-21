using System;
using System.Threading;

namespace Fpv450.Sim.Control
{
    /// <summary>
    /// The lock-free single-producer, single-consumer ring from the input thread to the flight thread (INP-04).
    /// The input thread only writes; the flight thread only consumes. Preallocated, no allocation per sample.
    /// </summary>
    public sealed class StickInbox
    {
        readonly StickSample[] _ring;
        readonly long _mask;
        long _written; // written by the producer only, read by the consumer
        long _read;    // written by the consumer only, read by the producer
        long _dropped;
        StickSample _inForce = StickSample.Neutral; // the consumer's

        /// <param name="capacity">A power of two. 1024 holds 4 s of reports at 250 Hz, so only a stall of the flight
        /// thread longer than that drops a report.</param>
        public StickInbox(int capacity = 1024)
        {
            if (capacity < 2 || (capacity & (capacity - 1)) != 0)
                throw new ArgumentException("capacity must be a power of two, at least 2", nameof(capacity));
            _ring = new StickSample[capacity];
            _mask = capacity - 1;
        }

        /// <summary>Reports refused because the ring was full. The log records the count.</summary>
        public long Dropped => Volatile.Read(ref _dropped);

        /// <summary>Input thread: queue one report. False (and counted in Dropped) when the ring is full.</summary>
        public bool TryWrite(in StickSample sample)
        {
            long w = _written;
            if (w - Volatile.Read(ref _read) > _mask)
            {
                Interlocked.Increment(ref _dropped);
                return false;
            }
            _ring[w & _mask] = sample;
            Volatile.Write(ref _written, w + 1);
            return true;
        }

        /// <summary>
        /// Flight thread: the sample in force at a step whose host time is stepHostUs, the latest report with
        /// HostUs ≤ stepHostUs (StickSample, "the timing contract"). Reports stamped later stay queued.
        /// </summary>
        public void Consume(long stepHostUs, out StickSample inForce)
        {
            long w = Volatile.Read(ref _written);
            long r = _read;
            while (r < w && _ring[r & _mask].HostUs <= stepHostUs)
            {
                _inForce = _ring[r & _mask];
                r++;
            }
            Volatile.Write(ref _read, r);
            inForce = _inForce;
        }
    }
}
