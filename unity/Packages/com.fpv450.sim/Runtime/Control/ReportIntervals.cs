namespace Fpv450.Sim.Control
{
    /// <summary>Where a flight's sticks came from, as the log header records it (INP-01, INP-06).</summary>
    public enum InputSource : byte
    {
        /// <summary>No input layer: a bench, a test or a replay.</summary>
        None = 0,

        /// <summary>An EdgeTX or OpenTX radio in USB joystick mode (the TX12).</summary>
        Radio = 1,

        /// <summary>Another HID joystick or gamepad.</summary>
        Joystick = 2,

        /// <summary>The keyboard: development only, never for tuning.</summary>
        Keyboard = 3,
    }

    /// <summary>
    /// The histogram of the intervals between a device's reports (INP-04), as the input layer measures it on arrival
    /// and the log header records it: fixed bins of BinUs, the last one also holding every longer interval.
    /// </summary>
    public sealed class ReportIntervals
    {
        public readonly int BinUs;
        public readonly long[] Counts;

        /// <summary>Intervals added, their sum and their extremes, µs.</summary>
        public long Count, SumUs, MinUs, MaxUs;

        /// <summary>100 µs bins to 20 ms: a USB frame is 1 ms, the TX12's mixer runs every 2 ms (EdgeTX 2.8).</summary>
        public ReportIntervals(int binUs = 100, int bins = 200)
        {
            BinUs = binUs;
            Counts = new long[bins];
        }

        public void Add(long us)
        {
            long bin = us / BinUs;
            Counts[bin < 0 ? 0 : bin >= Counts.Length ? Counts.Length - 1 : bin]++;
            if (Count == 0 || us < MinUs) MinUs = us;
            if (Count == 0 || us > MaxUs) MaxUs = us;
            Count++;
            SumUs += us;
        }

        /// <summary>The upper edge of the bin holding the given percentile (1–100) of the intervals, µs; 0 when empty.</summary>
        public long PercentileUs(int percent)
        {
            long rank = (Count * percent + 99) / 100, seen = 0;
            for (int i = 0; i < Counts.Length; i++)
            {
                seen += Counts[i];
                if (seen >= rank && seen > 0) return (long)(i + 1) * BinUs;
            }
            return 0;
        }
    }
}
