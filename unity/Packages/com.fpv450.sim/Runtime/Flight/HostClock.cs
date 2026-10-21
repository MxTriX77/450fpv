using System.Diagnostics;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The host's monotonic clock in µs. Only the FlightScheduler reads it, never a step (PHB-01); the input layer
    /// stamps StickSample.HostUs with the same clock on arrival.
    /// </summary>
    public interface IHostClock
    {
        long NowUs();
    }

    /// <summary>The process's Stopwatch (QueryPerformanceCounter on Windows) as µs.</summary>
    public sealed class HostClock : IHostClock
    {
        public static readonly HostClock Instance = new HostClock();

        HostClock()
        {
        }

        public long NowUs() => TicksToUs(Stopwatch.GetTimestamp());

        /// <summary>A Stopwatch timestamp as µs, without overflowing for any uptime.</summary>
        public static long TicksToUs(long ticks)
        {
            long f = Stopwatch.Frequency;
            return ticks / f * 1000000L + ticks % f * 1000000L / f;
        }
    }
}
