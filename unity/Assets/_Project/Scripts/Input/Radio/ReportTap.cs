using System;
using System.Threading;

namespace Fpv450.Input.Radio
{
    /// <summary>
    /// Every raw report, from the input thread to one reader (the Calibrate screen's wizard on the main thread): a
    /// lock-free single-producer, single-consumer ring of report copies, preallocated. The wizard sees each report with
    /// its arrival stamp, so its result never depends on the frame rate. A full ring drops the report and counts it.
    /// </summary>
    public sealed class ReportTap
    {
        readonly RadioReport[] _ring;
        readonly long _mask;
        long _written, _read, _dropped;

        /// <param name="capacity">A power of two; 4096 holds 8 s of a TX12's reports.</param>
        public ReportTap(int axes, int capacity = 4096)
        {
            if (capacity < 2 || (capacity & (capacity - 1)) != 0)
                throw new ArgumentException("capacity must be a power of two, at least 2", nameof(capacity));
            _ring = new RadioReport[capacity];
            for (int i = 0; i < capacity; i++) _ring[i] = new RadioReport(axes);
            _mask = capacity - 1;
        }

        public long Dropped => Volatile.Read(ref _dropped);

        /// <summary>Input thread: a copy of report. False (and counted) when the ring is full.</summary>
        public bool TryWrite(RadioReport report)
        {
            long w = _written;
            if (w - Volatile.Read(ref _read) > _mask)
            {
                Interlocked.Increment(ref _dropped);
                return false;
            }
            report.CopyTo(_ring[w & _mask]);
            Volatile.Write(ref _written, w + 1);
            return true;
        }

        /// <summary>The reader: the oldest report into into; false when none is waiting.</summary>
        public bool TryRead(RadioReport into)
        {
            long r = _read;
            if (r >= Volatile.Read(ref _written)) return false;
            _ring[r & _mask].CopyTo(into);
            Volatile.Write(ref _read, r + 1);
            return true;
        }
    }
}
