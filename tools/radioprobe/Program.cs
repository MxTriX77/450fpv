using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Fpv450.Input.Radio;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.RadioProbe
{
    // The radio on the bench, without Unity (p6-input-app 1.1, INP-01, INP-04):
    //   radioprobe                      the HID joysticks and gamepads present, radios first
    //   radioprobe --measure [s]        the app's input thread (RadioReader) on the first of them for s seconds (10):
    //                                   its axes 4 times a second, then the report-interval histogram the log records
    //   radioprobe --record <file> [s]  reads the first one for s seconds and saves the stream as a recording for the
    //                                   tests (RecordedRadio's text), stamps in µs from the start
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
            Console.WriteLine("usage: radioprobe [--measure [seconds] | --record <file> [seconds]]");
            return 1;
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
