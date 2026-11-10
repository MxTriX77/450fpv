using System.Globalization;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// A flight's step times (p7h-step-budget 1.1, PLT-04): how long each step's FlightCore.Advance took on the host
    /// clock, as the scheduler measured it. Only the log's end block and the player's line read them: timing never
    /// feeds the state.
    /// </summary>
    public struct StepTimeSummary
    {
        /// <summary>Steps timed.</summary>
        public long Count;

        /// <summary>Steps whose advance took longer than a step's 1 ms (FlightCore.StepUs).</summary>
        public long Overruns;

        /// <summary>The nearest-rank percentiles, µs; at most the max.</summary>
        public long P50Us, P99Us, P999Us;

        /// <summary>The slowest step, µs.</summary>
        public long MaxUs;

        public override string ToString()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return Count.ToString(c) + " steps, " + Overruns.ToString(c) + " over 1 ms, p50 " + P50Us.ToString(c) + " us, p99 " + P99Us.ToString(c)
                + " us, p99.9 " + P999Us.ToString(c) + " us, max " + MaxUs.ToString(c) + " us";
        }
    }

    /// <summary>
    /// The scheduler's step timer (p7h-step-budget 1.1): a fixed-bin histogram of each step's advance in µs, its count,
    /// overruns and max. Add allocates nothing; the bins are allocated once. Written on the flight thread; read it (Summary)
    /// once that thread has stopped. Reset starts a new flight.
    /// </summary>
    public sealed class StepTimes
    {
        /// <summary>
        /// The bins are the host clock's µs (HostClock), so a percentile below TopUs is exact; every step from TopUs on
        /// shares the top bin, whose percentile reads as the max. Assumed: ten steps' budgets, 80 KB.
        /// </summary>
        public const int TopUs = 10 * (int)FlightCore.StepUs;

        readonly long[] _bins = new long[TopUs + 1];
        long _count, _overruns, _maxUs;

        /// <summary>One step's advance, µs on the host clock (monotonic, so never negative).</summary>
        public void Add(long us)
        {
            _bins[us < TopUs ? us : TopUs]++;
            _count++;
            if (us > FlightCore.StepUs) _overruns++;
            if (us > _maxUs) _maxUs = us;
        }

        public void Reset()
        {
            System.Array.Clear(_bins, 0, _bins.Length);
            _count = _overruns = _maxUs = 0;
        }

        public StepTimeSummary Summary() => new StepTimeSummary
        {
            Count = _count,
            Overruns = _overruns,
            P50Us = Percentile(500),
            P99Us = Percentile(990),
            P999Us = Percentile(999),
            MaxUs = _maxUs,
        };

        // The nearest-rank percentile in thousandths: the smallest bin that holds at least ceil(count × permille / 1000)
        // steps at or below it; 0 with no steps.
        long Percentile(long permille)
        {
            long rank = (_count * permille + 999) / 1000, seen = 0;
            for (int us = 0; us < TopUs; us++)
            {
                seen += _bins[us];
                if (seen >= rank && seen > 0) return us;
            }
            return _maxUs;
        }
    }
}
