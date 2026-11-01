using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.Input.Radio
{
    /// <summary>
    /// A recorded report stream played at the device boundary (design 3): each report with the HostClock µs it arrived
    /// at, and the device's loss if the recording has one. Read hands a report over once the clock reaches its stamp less
    /// LeadUs, stamped as recorded, so a flight replays the same sticks at the same times whatever the threads do; with a
    /// virtual clock and a timeout of 0 it never blocks, for a test that steps time itself. Save and Load keep a live
    /// device's stream as text (tools/radioprobe --record).
    /// </summary>
    public sealed class RecordedRadio : IRadioDevice
    {
        const string Magic = "fpv450 radio recording 1";

        readonly List<long> _times = new List<long>();
        readonly List<int[]> _axes = new List<int[]>();
        readonly List<ulong> _buttons = new List<ulong>();
        int _next;

        public RecordedRadio(string name, InputSource source, RadioAxis[] axes, int buttons)
        {
            Name = name ?? "";
            Source = source;
            Axes = axes ?? throw new ArgumentNullException(nameof(axes));
            ButtonCount = buttons;
        }

        public string Name { get; }
        public InputSource Source { get; }
        public RadioAxis[] Axes { get; }
        public int ButtonCount { get; }

        /// <summary>The clock Read paces on; the stamps are in its µs.</summary>
        public IHostClock Clock;

        /// <summary>How long before its stamp a report is handed over, µs.</summary>
        public long LeadUs;

        /// <summary>When the device is lost (its cable pulled), µs; −1 for never.</summary>
        public long LostAtUs = -1;

        public int Count => _times.Count;

        public long TimeOf(int i) => _times[i];

        public int[] AxesOf(int i) => _axes[i];

        public ulong ButtonsOf(int i) => _buttons[i];

        /// <summary>Appends a report; stamps must not decrease.</summary>
        public void Add(long hostUs, int[] axes, ulong buttons)
        {
            if (axes.Length != Axes.Length) throw new ArgumentException("a report has " + axes.Length + " axes, the device " + Axes.Length);
            if (_times.Count > 0 && hostUs < _times[_times.Count - 1]) throw new ArgumentException("the stamps decrease at " + hostUs);
            _times.Add(hostUs);
            _axes.Add((int[])axes.Clone());
            _buttons.Add(buttons);
        }

        public RadioRead Read(RadioReport report, int timeoutMs)
        {
            bool lost = _next >= _times.Count || (LostAtUs >= 0 && _times[_next] >= LostAtUs);
            if (lost && LostAtUs < 0)
            {
                if (timeoutMs > 0) Thread.Sleep(timeoutMs); // the recording is over: a silent device
                return RadioRead.Timeout;
            }
            long stamp = lost ? LostAtUs : _times[_next];
            if (!WaitUntil(stamp - LeadUs, timeoutMs)) return RadioRead.Timeout;
            report.HostUs = stamp;
            if (lost) return RadioRead.Lost;
            Array.Copy(_axes[_next], report.Axes, Math.Min(report.Axes.Length, Axes.Length));
            report.Buttons = _buttons[_next];
            _next++;
            return RadioRead.Report;
        }

        // Spins near the time, since a 1 ms sleep may last a 15.6 ms timer tick.
        bool WaitUntil(long dueUs, int timeoutMs)
        {
            long now = Clock.NowUs(), deadline = now + timeoutMs * 1000L;
            while (now < dueUs)
            {
                if (now >= deadline) return false;
                if (dueUs - now > 20000 && deadline - now > 20000) Thread.Sleep(1);
                else Thread.SpinWait(64);
                now = Clock.NowUs();
            }
            return true;
        }

        public void Dispose()
        {
        }

        /// <summary>The recording as text: a line per report (stamp µs, the axes, the buttons in hex) and its loss.</summary>
        public void Save(TextWriter w)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            w.WriteLine(Magic);
            w.WriteLine("device " + Name);
            w.WriteLine("source " + Source.ToString().ToLowerInvariant());
            var axes = new List<string>();
            foreach (RadioAxis a in Axes) axes.Add(a.Page.ToString(c) + ":" + a.Usage.ToString(c) + ":" + a.Min.ToString(c) + ":" + a.Max.ToString(c));
            w.WriteLine("axes " + string.Join(" ", axes));
            w.WriteLine("buttons " + ButtonCount.ToString(c));
            for (int i = 0; i < _times.Count; i++)
            {
                var line = new List<string> { "r", _times[i].ToString(c) };
                foreach (int v in _axes[i]) line.Add(v.ToString(c));
                line.Add(_buttons[i].ToString("X", c));
                w.WriteLine(string.Join(" ", line));
            }
            if (LostAtUs >= 0) w.WriteLine("lost " + LostAtUs.ToString(c));
        }

        public static RecordedRadio Load(TextReader r)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            if (r.ReadLine() != Magic) throw new FormatException("not a radio recording");
            string name = After(r.ReadLine(), "device ");
            var source = (InputSource)Enum.Parse(typeof(InputSource), After(r.ReadLine(), "source "), true);
            var axes = new List<RadioAxis>();
            foreach (string a in After(r.ReadLine(), "axes ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = a.Split(':');
                axes.Add(new RadioAxis(ushort.Parse(p[0], c), ushort.Parse(p[1], c), int.Parse(p[2], c), int.Parse(p[3], c)));
            }
            var rec = new RecordedRadio(name, source, axes.ToArray(), int.Parse(After(r.ReadLine(), "buttons "), c));
            var values = new int[axes.Count];
            string line;
            while ((line = r.ReadLine()) != null)
            {
                string[] p = line.Split(' ');
                if (p[0] == "lost")
                {
                    rec.LostAtUs = long.Parse(p[1], c);
                    continue;
                }
                if (p[0] != "r" || p.Length != axes.Count + 3) throw new FormatException("a bad report line: " + line);
                for (int i = 0; i < values.Length; i++) values[i] = int.Parse(p[2 + i], c);
                rec.Add(long.Parse(p[1], c), values, ulong.Parse(p[p.Length - 1], NumberStyles.HexNumber, c));
            }
            return rec;
        }

        static string After(string line, string key)
        {
            if (line == null || !line.StartsWith(key, StringComparison.Ordinal)) throw new FormatException("expected '" + key.Trim() + "'");
            return line.Substring(key.Length);
        }
    }
}
