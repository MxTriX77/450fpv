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

    /// <summary>
    /// The latency test's two figures (p6-input-app design 3, INP-04), as the log header records them: a known stick
    /// step injected as reports at the device boundary, timed from each step report's arrival stamp (HostUs)
    /// - FirstStep: to the host time of the first flight step whose stick in force is that report or a later one
    /// - Motor: to the motor command's response, the ESCs' mean duty to their motors (the controller's DShot command
    ///   through the ESC's lag) halfway through the step's change, interpolated between the steps' records
    /// </summary>
    public sealed class InputLatency
    {
        /// <summary>Stick steps timed.</summary>
        public long Steps;

        public LatencySpread FirstStep, Motor;
    }

    /// <summary>A latency's spread over the steps timed, µs: the nearest-rank median and 99th percentile.</summary>
    public struct LatencySpread
    {
        public long MinUs, P50Us, P99Us, MaxUs;
        public double MeanUs;

        /// <summary>The spread of the first count values (it sorts a copy; it allocates).</summary>
        public static LatencySpread Of(long[] values, int count)
        {
            if (count <= 0) return default;
            var sorted = new long[count];
            System.Array.Copy(values, sorted, count);
            System.Array.Sort(sorted);
            double sum = 0;
            foreach (long v in sorted) sum += v;
            return new LatencySpread
            {
                MinUs = sorted[0],
                P50Us = sorted[(count * 50 + 99) / 100 - 1],
                P99Us = sorted[(count * 99 + 99) / 100 - 1],
                MaxUs = sorted[count - 1],
                MeanUs = sum / count,
            };
        }
    }
}
