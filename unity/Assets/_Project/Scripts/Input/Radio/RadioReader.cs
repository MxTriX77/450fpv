using System;
using System.Threading;
using Fpv450.Sim.Control;

namespace Fpv450.Input.Radio
{
    /// <summary>
    /// The input thread (design 1, INP-04, INP-05): it reads the radio, never the render thread, and hands each report to
    /// the flight thread as a StickSample through the flight's StickInbox, lock-free.
    /// - **Stamps:** each report carries the HostClock µs the device stamped on its arrival; the sample keeps it, so the
    ///   step that takes it is fixed by when it arrived, never by when a frame ran.
    /// - **The map:** raw values through RawStickMap until the calibration (p6-input-app 1.2) replaces it.
    /// - **Loss:** a lost device is a failsafe sample (StickSample.Failsafe: centred, throttle down, disarmed) stamped
    ///   when the loss was seen, then a rescan every 0.5 s until a device opens again.
    /// - **Intervals:** every arrival stamp goes into a ring, so Intervals gives the histogram of the last window.
    /// - **Latest:** the last report for a live display, read from any thread.
    /// Steady state allocates nothing. Pump is one pass of the thread, for tests that drive it on a virtual clock.
    /// </summary>
    public sealed class RadioReader : IDisposable
    {
        const int StampCapacity = 1 << 15; // 65 s of reports at 2 ms, 32 s at 1 ms
        const long StampMask = StampCapacity - 1;
        const int ReadTimeoutMs = 100, RescanMs = 500;

        readonly Func<IRadioDevice> _open;
        readonly RawStickMap _map;
        readonly long[] _stamps = new long[StampCapacity];
        StickInbox _inbox;
        IRadioDevice _device;
        RadioReport _report;
        Thread _thread;
        volatile bool _stop;
        volatile bool _connected;
        long _count, _sessionFrom, _losses;

        // Latest, under a sequence lock: odd while the input thread writes it.
        RadioReport _latest = new RadioReport(0);
        int _latestSeq;

        /// <param name="open">Opens a device, or returns null when none is present (WinHid.OpenFirst on a live machine).</param>
        public RadioReader(Func<IRadioDevice> open, RawStickMap map = null)
        {
            _open = open ?? throw new ArgumentNullException(nameof(open));
            _map = map ?? new RawStickMap();
        }

        /// <summary>The flight's inbox, or null between flights. The input thread is its only producer.</summary>
        public StickInbox Inbox
        {
            get => Volatile.Read(ref _inbox);
            set => Volatile.Write(ref _inbox, value);
        }

        /// <summary>A device is open and has not been lost.</summary>
        public bool Connected => _connected;

        /// <summary>The open (or last) device's product name and source.</summary>
        public string DeviceName { get; private set; } = "";

        public InputSource Source { get; private set; }

        /// <summary>The open (or last) device's axes, for a display's ranges.</summary>
        public RadioAxis[] Axes { get; private set; } = new RadioAxis[0];

        /// <summary>Reports delivered since the reader was made, and devices lost.</summary>
        public long Reports => Volatile.Read(ref _count);

        public long Losses => Volatile.Read(ref _losses);

        /// <summary>Starts the input thread.</summary>
        public void Start()
        {
            if (_thread != null) throw new InvalidOperationException("the input thread is running");
            _stop = false;
            _thread = new Thread(Run) { Name = "fpv450 input", IsBackground = true, Priority = ThreadPriority.Highest };
            _thread.Start();
        }

        /// <summary>Stops the input thread, waits for it and closes the device.</summary>
        public void Stop()
        {
            _stop = true;
            _thread?.Join();
            _thread = null;
            Close();
        }

        public void Dispose() => Stop();

        void Run()
        {
            while (!_stop)
            {
                if (!Pump(ReadTimeoutMs) && _device == null)
                    for (int i = 0; i < RescanMs / 50 && !_stop; i++)
                        Thread.Sleep(50);
            }
        }

        /// <summary>
        /// One pass of the input thread: opens a device if none is open, then waits up to timeoutMs for its next report
        /// and delivers it, or the loss. True when a report or a loss was delivered.
        /// </summary>
        public bool Pump(int timeoutMs)
        {
            if (_device == null && !TryOpen()) return false;
            switch (_device.Read(_report, timeoutMs))
            {
                case RadioRead.Report:
                    Deliver(_report);
                    return true;
                case RadioRead.Lost:
                    Lose(_report.HostUs);
                    Close();
                    return true;
                default:
                    return false;
            }
        }

        bool TryOpen()
        {
            IRadioDevice d = _open();
            if (d == null) return false;
            _device = d;
            _report = new RadioReport(d.Axes.Length);
            DeviceName = d.Name ?? "";
            Source = d.Source;
            Axes = d.Axes;
            Interlocked.Increment(ref _latestSeq);
            Volatile.Write(ref _latest, new RadioReport(d.Axes.Length));
            Volatile.Write(ref _latestSeq, _latestSeq + 1);
            Volatile.Write(ref _sessionFrom, _count); // the intervals of this device only
            _connected = true;
            return true;
        }

        void Close()
        {
            _device?.Dispose();
            _device = null;
        }

        void Deliver(RadioReport report)
        {
            long n = _count;
            _stamps[n & StampMask] = report.HostUs;
            Volatile.Write(ref _count, n + 1);

            Interlocked.Increment(ref _latestSeq);
            report.CopyTo(_latest);
            Volatile.Write(ref _latestSeq, _latestSeq + 1);

            _map.Map(report, _device.Axes, out StickSample sample);
            sample.HostUs = report.HostUs;
            Volatile.Read(ref _inbox)?.TryWrite(sample);
        }

        void Lose(long hostUs)
        {
            _connected = false;
            Interlocked.Increment(ref _losses);
            StickSample failsafe = StickSample.Neutral;
            failsafe.HostUs = hostUs;
            failsafe.Failsafe = true;
            Volatile.Read(ref _inbox)?.TryWrite(failsafe);
        }

        /// <summary>The last report into into (any thread); false before the first.</summary>
        public bool ReadLatest(RadioReport into)
        {
            while (true)
            {
                int before = Volatile.Read(ref _latestSeq);
                if ((before & 1) != 0)
                {
                    Thread.SpinWait(20);
                    continue;
                }
                Volatile.Read(ref _latest).CopyTo(into);
                Thread.MemoryBarrier();
                if (Volatile.Read(ref _latestSeq) == before) return Reports > 0;
            }
        }

        /// <summary>
        /// The histogram of the open (or last) device's report intervals over its last windowUs, from the input thread's
        /// arrival stamps (any thread; it allocates). The flight session records it in the log header at the start.
        /// </summary>
        public ReportIntervals Intervals(long windowUs)
        {
            while (true)
            {
                var result = new ReportIntervals();
                long n = Volatile.Read(ref _count), from = Math.Max(Volatile.Read(ref _sessionFrom), n - StampCapacity / 2);
                if (n - from < 2) return result;
                long last = _stamps[(n - 1) & StampMask];
                long first = n - 1;
                while (first > from && _stamps[(first - 1) & StampMask] >= last - windowUs) first--;
                var copy = new long[n - first];
                for (long i = first; i < n; i++) copy[i - first] = _stamps[i & StampMask];
                Thread.MemoryBarrier();
                if (Volatile.Read(ref _count) - first >= StampCapacity) continue; // the input thread lapped the copy
                for (int i = 1; i < copy.Length; i++) result.Add(copy[i] - copy[i - 1]);
                return result;
            }
        }
    }
}
