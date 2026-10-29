using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Fpv450.Input.Radio;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;

namespace Fpv450.RadioProbe
{
    // The radio on the bench, without Unity (p6-input-app 1.1, 1.5, INP-01, INP-04):
    //   radioprobe                      the HID joysticks and gamepads present, radios first
    //   radioprobe --measure [s]        the app's input thread (RadioReader) on the first of them for s seconds (10):
    //                                   its axes 4 times a second, then the report-interval histogram the log records
    //   radioprobe --record <file> [s]  reads the first one for s seconds and saves the stream as a recording for the
    //                                   tests (RecordedRadio's text), stamps in µs from the start
    //   radioprobe --latency [n] [--from <file>]
    //                                   the latency test (LatencyTest) on the first one, its sticks left alone (the test
    //                                   replaces their values at the device boundary), or on a recording played at its
    //                                   stamps: n stick steps (120) on the repository's drone, about 11 s; prints the
    //                                   report interval and both figures, and writes the test flight's log
    // Exit codes: 0 done, 1 bad arguments, 2 no device.
    static class Program
    {
        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        static int Main(string[] args)
        {
            var devices = WinHid.List();
            Console.WriteLine(devices.Count + " joystick(s) present");
            foreach (WinHidInfo d in devices)
                Console.WriteLine("  " + d.Source.ToString().ToLowerInvariant() + " '" + d.Name + "' " + d.VendorId.ToString("X4", C) + ":"
                    + d.ProductId.ToString("X4", C));
            if (args.Length == 0) return 0;
            if (args[0] == "--latency") return Latency(args, devices.Count);

            bool record = args[0] == "--record";
            if (!record && args[0] != "--measure") return Usage();
            int at = record ? 2 : 1;
            if (record && args.Length < 2) return Usage();
            double seconds = args.Length > at ? double.Parse(args[at], C) : 10;
            if (devices.Count == 0)
            {
                Console.WriteLine("no radio or joystick to read: plug the TX12 in and pick USB joystick mode");
                return 2;
            }
            return record ? Record(args[1], seconds) : Measure(seconds);
        }

        static int Usage()
        {
            Console.WriteLine("usage: radioprobe [--measure [seconds] | --record <file> [seconds] | --latency [steps] [--from <recording>]]");
            return 1;
        }

        static int Latency(string[] args, int devices)
        {
            int changes = 120;
            string from = null;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--from" && i + 1 < args.Length) from = args[++i];
                else if (!int.TryParse(args[i], NumberStyles.None, C, out changes) || changes < 1) return Usage();
            }
            string drone = FindUp(Path.Combine("assets-src", "drone", "drone.json"));
            if (drone == null)
            {
                Console.WriteLine("assets-src/drone/drone.json was not found up from the current folder: run from the repository");
                return 1;
            }
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(drone));

            IRadioDevice device;
            IHostClock clock;
            long startUs, needUs = LatencyTest.PreRollUs + LatencyTest.DurationUs(changes);
            if (from != null)
            {
                RecordedRadio rec;
                using (var r = new StreamReader(from)) rec = RecordedRadio.Load(r);
                long heldUs = rec.Count == 0 ? 0 : rec.TimeOf(rec.Count - 1) - rec.TimeOf(0);
                if (heldUs < needUs)
                {
                    Console.WriteLine("the recording holds " + (heldUs / 1e6).ToString("0.0", C) + " s; " + changes + " steps need "
                        + (needUs / 1e6).ToString("0.0", C) + " s");
                    return 1;
                }
                clock = new RecordingClock(HostClock.Instance.NowUs() - rec.TimeOf(0) + 20000);
                rec.Clock = clock;
                device = rec;
                startUs = rec.TimeOf(0) + LatencyTest.PreRollUs;
            }
            else
            {
                if (devices == 0)
                {
                    Console.WriteLine("no radio or joystick to read: plug the TX12 in and pick USB joystick mode, or give --from <recording>");
                    return 2;
                }
                device = WinHid.OpenFirst(HostClock.Instance);
                if (device == null)
                {
                    Console.WriteLine("no device opened for reading");
                    return 2;
                }
                clock = HostClock.Instance;
                startUs = clock.NowUs() + LatencyTest.PreRollUs;
            }

            Console.WriteLine("latency test: " + changes + " throttle steps " + LatencyTest.Low.ToString("0.00", C) + " / " + LatencyTest.High.ToString("0.00", C)
                + " every " + LatencyTest.HoldUs / 1000 + " ms on '" + spec.Name + "', " + (needUs / 1e6).ToString("0.0", C) + " s");
            FcSettings settings = FcSettings.Default;
            LatencyTest t;
            string path = LogStore.NewPath(LatencyTest.Bench(spec, settings).Seed);
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                t = LatencyTest.Run(device, spec, settings, clock, startUs, changes, file, "radioprobe");

            Console.WriteLine("read '" + t.DeviceName + "' (" + t.Source.ToString().ToLowerInvariant() + "): " + t.Steps + " steps, " + t.Hitches + " hitches, "
                + t.Dropped + " reports dropped; " + t.Timed + " of " + changes + " steps timed, " + t.Limited + " at an ESC's current limit");
            Console.WriteLine("before the flight:");
            Print(t.Intervals);
            Console.WriteLine("from each step report's arrival:");
            Console.WriteLine("  to the first step that uses it: " + Spread(t.Figures.FirstStep));
            Console.WriteLine("  to the motor command (the ESCs' duty halfway): " + Spread(t.Figures.Motor));
            Console.WriteLine("    the RC link's next frame: " + Spread(t.FrameWait));
            Console.WriteLine("    then the RC smoothing and the mixer to the DShot command halfway: " + Spread(t.Smoothing));
            Console.WriteLine("    then the ESC's lag: " + Spread(t.Esc));
            Console.WriteLine("the test flight's log, " + Path.GetFileName(path) + " in the flights folder (LogStore):");
            FlightSummary.Read(path).Print(Console.Out, 0);
            return 0;
        }

        static string Spread(in LatencySpread s) =>
            "p50 " + Ms(s.P50Us) + ", p99 " + Ms(s.P99Us) + " ms (min " + Ms(s.MinUs) + ", max " + Ms(s.MaxUs) + ", mean " + (s.MeanUs / 1000).ToString("0.000", C) + ")";

        // A repository file, up from the current folder.
        static string FindUp(string relative)
        {
            for (DirectoryInfo d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, relative)))
                    return Path.Combine(d.FullName, relative);
            return null;
        }

        // The host clock shifted so a recording's µs run in real time.
        sealed class RecordingClock : IHostClock
        {
            readonly long _zero;

            public RecordingClock(long zero) => _zero = zero;

            public long NowUs() => HostClock.Instance.NowUs() - _zero;
        }

        static int Measure(double seconds)
        {
            var reader = new RadioReader(() => WinHid.OpenFirst(HostClock.Instance));
            reader.Start();
            var latest = new RadioReport(0);
            long start = HostClock.Instance.NowUs(), window = (long)(seconds * 1e6);
            try
            {
                while (HostClock.Instance.NowUs() - start < window)
                {
                    Thread.Sleep(250);
                    if (latest.Axes.Length != reader.Axes.Length) latest = new RadioReport(reader.Axes.Length);
                    if (!reader.ReadLatest(latest))
                    {
                        Console.WriteLine("  waiting for a report");
                        continue;
                    }
                    var line = new StringBuilder("  " + ((HostClock.Instance.NowUs() - start) / 1e6).ToString("0.00", C) + " s:");
                    foreach (int v in latest.Axes) line.Append(' ').Append(v.ToString(C));
                    line.Append(" buttons ").Append(latest.Buttons.ToString("X", C)).Append(reader.Connected ? "" : " (LOST)");
                    Console.WriteLine(line.ToString());
                }
            }
            finally
            {
                reader.Stop();
            }
            Console.WriteLine("read '" + reader.DeviceName + "' (" + reader.Source.ToString().ToLowerInvariant() + "): " + reader.Reports + " reports, "
                + reader.Losses + " lost");
            Print(reader.Intervals(window));
            return 0;
        }

        static int Record(string path, double seconds)
        {
            WinHidDevice d = WinHid.OpenFirst(HostClock.Instance);
            if (d == null)
            {
                Console.WriteLine("no device opened for reading");
                return 2;
            }
            var rec = new RecordedRadio(d.Name, d.Source, d.Axes, d.ButtonCount);
            var report = new RadioReport(d.Axes.Length);
            var intervals = new ReportIntervals();
            long start = HostClock.Instance.NowUs(), end = start + (long)(seconds * 1e6), last = -1;
            try
            {
                while (HostClock.Instance.NowUs() < end)
                {
                    RadioRead r = d.Read(report, 100);
                    if (r == RadioRead.Lost)
                    {
                        rec.LostAtUs = report.HostUs - start;
                        Console.WriteLine("the device was lost at " + (rec.LostAtUs / 1e6).ToString("0.000", C) + " s");
                        break;
                    }
                    if (r != RadioRead.Report) continue;
                    if (last >= 0) intervals.Add(report.HostUs - last);
                    last = report.HostUs;
                    rec.Add(report.HostUs - start, report.Axes, report.Buttons);
                }
            }
            finally
            {
                d.Dispose();
            }
            using (var w = new StreamWriter(path)) rec.Save(w);
            Console.WriteLine("recorded '" + d.Name + "': " + rec.Count + " reports to " + Path.GetFileName(path));
            Print(intervals);
            return 0;
        }

        static void Print(ReportIntervals r)
        {
            if (r.Count == 0)
            {
                Console.WriteLine("no report intervals");
                return;
            }
            Console.WriteLine("report interval: " + r.Count + " intervals, mean " + ((double)r.SumUs / r.Count / 1000).ToString("0.000", C) + " ms, min "
                + Ms(r.MinUs) + ", p50 " + Ms(r.PercentileUs(50)) + ", p90 " + Ms(r.PercentileUs(90)) + ", p99 " + Ms(r.PercentileUs(99)) + ", max "
                + Ms(r.MaxUs) + " ms (percentiles are " + r.BinUs + " us bins' upper edges)");
            for (int i = 0; i < r.Counts.Length; i++)
                if (r.Counts[i] > 0)
                    Console.WriteLine("  " + Ms((long)i * r.BinUs) + (i == r.Counts.Length - 1 ? "+" : "-" + Ms((long)(i + 1) * r.BinUs)) + " ms: " + r.Counts[i]);
        }

        static string Ms(long us) => (us / 1000.0).ToString("0.000", C);
    }
}
