using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Fpv450.Input.Radio;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using NUnit.Framework;

namespace Fpv450.Tests.Radio
{
    // tx-input (p6-input-app 1.5, design 3, INP-04): "Latency is measured" on a synthetic EdgeTX stream at 2 ms
    // (Tx12Stream's timing, played at its arrival stamps as a live device hands reports over), through the injector,
    // the input thread, the inbox and the flight thread on the repository's drone, in real time. Both figures over 128
    // stick steps, each change's parts explained: the 1 ms step, the 250 Hz RC link's frame, the RC smoothing with the
    // throttle boost against Betaflight's formulas, the ESC's lag against drone.json's. Then the figures in the log's
    // header and summary, the first figure again from the log's records alone, and the log replayed bit for bit.
    public sealed class LatencyTests
    {
        const int Changes = 120;
        const long StartUs = 1000000; // the stream's µs of step 0: a second of reports before the flight

        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        [Test]
        public void LatencyIsMeasuredOnASyntheticTx12()
        {
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(RepositoryFile("assets-src/drone/drone.json")));
            RecordedRadio radio = Tx12Stream.Make(0, StartUs + LatencyTest.DurationUs(Changes) + 100000);
            var clock = new ShiftedClock(20000);
            radio.Clock = clock; // LeadUs 0: each report is handed over at its arrival stamp
            var log = new MemoryStream();
            LatencyTest t = LatencyTest.Run(radio, spec, FcSettings.Default, clock, StartUs, Changes, log, "inputtests");

            TestContext.WriteLine("'" + t.DeviceName + "': " + t.Steps + " steps, " + t.Hitches + " hitches, " + t.Dropped + " dropped; "
                + t.Intervals.Count + " report intervals before the flight, mean " + ((double)t.Intervals.SumUs / t.Intervals.Count / 1000).ToString("0.000", C) + " ms");
            TestContext.WriteLine(Changes + " changes, " + t.Timed + " timed, " + t.Limited + " at an ESC's current limit");
            TestContext.WriteLine("  to the first step: " + Spread(t.Figures.FirstStep));
            TestContext.WriteLine("  to the motor command (the ESCs' duty halfway): " + Spread(t.Figures.Motor));
            TestContext.WriteLine("  of which the RC frame's wait " + Spread(t.FrameWait) + ", the smoothing to the DShot command halfway "
                + Spread(t.Smoothing) + ", the ESC " + Spread(t.Esc));

            Assert.That(t.Timed, Is.EqualTo(Changes), "every change timed");
            Assert.That(t.Figures.Steps, Is.EqualTo(Changes));
            Assert.That((t.Limited, t.Dropped), Is.EqualTo((0, 0L)), "no ESC at its current limit, no report dropped");

            // Each change's parts. The first: by the timing contract, a report waits for the next 1 ms step (one more if
            // the input thread had not delivered it when that step ran). The frame: the RC link takes the stick in force
            // every 4th step, and 2 ms reports land on two of its four phases.
            ExpectedChain(spec, out long smoothing, out long esc, out double cutoffHz);
            TestContext.WriteLine("  Betaflight's chain at " + cutoffHz + " Hz with the boost, then the ESC: the smoothing " + Ms(smoothing) + " ms, the ESC "
                + Ms(esc) + " ms");
            var waits = new HashSet<long>();
            int raced = 0;
            for (int i = 0; i < t.Timed; i++)
            {
                Assert.That(t.FirstStepUs[i], Is.InRange(0, 2 * FlightCore.StepUs - 1), "change " + i + ": arrival to the first step");
                if (t.FirstStepUs[i] >= FlightCore.StepUs) raced++;
                else waits.Add(t.FrameWaitUs[i]); // a change that waited a second step meets the frame a step later
                Assert.That(t.FrameWaitUs[i] % FlightCore.StepUs, Is.EqualTo(0));
                Assert.That(t.FrameWaitUs[i], Is.InRange(0, 3 * FlightCore.StepUs), "change " + i + ": the frame's wait");
                Assert.That(t.SmoothingUs[i], Is.EqualTo(smoothing).Within(10), "change " + i + ": the RC smoothing against Betaflight's formulas");
                Assert.That(t.EscUs[i], Is.EqualTo(esc).Within(10), "change " + i + ": the ESC against its lag");
                Assert.That(t.MotorUs[i], Is.EqualTo(t.FirstStepUs[i] + t.FrameWaitUs[i] + t.SmoothingUs[i] + t.EscUs[i]));
            }
            TestContext.WriteLine("  " + raced + " changes waited a second step; the frame waits seen: " + string.Join(", ", Sorted(waits)) + " us");
            Assert.That(waits.Count, Is.EqualTo(2), "2 ms reports on a 4 ms frame: two phases");

            // The log: the figures in its header and its summary.
            byte[] bytes = log.ToArray();
            FlightSummary summary = FlightSummary.Read(new MemoryStream(bytes));
            Assert.That(summary.Complete, summary.CutReason);
            LogHeader h = summary.Header;
            Assert.That(h.StartHostUs, Is.EqualTo(StartUs));
            Assert.That(h.InputLatency, Is.Not.Null);
            Assert.That(h.InputLatency.Steps, Is.EqualTo(t.Figures.Steps));
            Assert.That(h.InputLatency.FirstStep, Is.EqualTo(t.Figures.FirstStep));
            Assert.That(h.InputLatency.Motor, Is.EqualTo(t.Figures.Motor));
            Assert.That(h.InputIntervals.Count, Is.InRange(450, 500), "the reports of the second before the header");
            Assert.That((double)h.InputIntervals.SumUs / h.InputIntervals.Count, Is.EqualTo(2000).Within(5));
            var text = new StringWriter();
            summary.Print(text, 0);
            TestContext.WriteLine(text.ToString());
            StringAssert.Contains("latency test: " + Changes + " stick steps, from each step report's arrival: to the first step that uses it p50 "
                + Ms(t.Figures.FirstStep.P50Us) + ", p99 " + Ms(t.Figures.FirstStep.P99Us), text.ToString());
            StringAssert.Contains("to the motor command (the ESCs' duty halfway through the change) p50 " + Ms(t.Figures.Motor.P50Us) + ", p99 "
                + Ms(t.Figures.Motor.P99Us), text.ToString());

            // The first figure from the records alone: each throttle change's step, its stick's stamp and the header's
            // step-0 time. The first change (0 to Low) is the pattern's start and the last ends the last hold.
            var ages = new List<long>();
            double throttle = 0;
            using (var r = new FlightLogReader(new MemoryStream(bytes)))
                while (r.Next())
                    if (r.Kind == LogFormat.StepBlock)
                    {
                        r.ReadStep(out LoggedStep s);
                        if (s.Stick.Throttle != throttle) ages.Add(h.StartHostUs + (s.State.Step - 1) * FlightCore.StepUs - s.Stick.HostUs);
                        throttle = s.Stick.Throttle;
                    }
            Assert.That(ages.Count, Is.EqualTo(Changes + 2));
            Assert.That(ages.GetRange(1, Changes), Is.EqualTo(t.FirstStepUs), "the first figure from the log's records and StartHostUs");

            ReplayResult replay = FlightReplay.Run(new MemoryStream(bytes), id => id == "floor" ? FlightWorld.Floor() : null, spec.ContentHash);
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
        }

        // A unit step of the throttle channel at an RC frame through Betaflight 4.5's chain, written out from its formulas
        // (common/filter.c pt1FilterGain and pt3FilterGain's order correction; rc.c's auto cutoff for the throttle;
        // mixer.c's throttle boost) at the 4 kHz loop, then drone.json's ESC filter at the loop's 250 µs, sampled as the
        // log samples them (each step's last loop) and crossed halfway as LatencyTest does: from the frame to the DShot
        // command halfway, and from there to the ESC's duty halfway.
        static void ExpectedChain(DroneSpec spec, out long smoothingUs, out long escUs, out double cutoffHz)
        {
            FcSettings s = FcSettings.Default;
            cutoffHz = Math.Max(15, (int)(250 * (1.5 / (1 + s.rc_smoothing_auto_factor_throttle / 10))));
            Assert.That(new FlightController(s, new bool[4]).ThrottleCutoffHz, Is.EqualTo(cutoffHz), "the model's throttle cutoff");
            double dt = 1.0 / 4000, tau = spec.BuildPropulsion().Motors[0].EscTau;
            double k3 = Gain(cutoffHz / Math.Sqrt(Math.Pow(2, 1.0 / 3) - 1), dt), kb = Gain(s.throttle_boost_cutoff, dt), boost = s.throttle_boost * 0.1;
            double a = 0, b = 0, x = 0, lpf = 0, e = 0;
            var command = new List<double> { 0 };
            var duty = new List<double> { 0 };
            for (int loop = 0; loop < 200; loop++)
            {
                a += k3 * (1 - a);
                b += k3 * (a - b);
                x += k3 * (b - x);
                lpf += kb * (x - lpf);
                double u = x + boost * (x - lpf);
                e += (u - e) * dt / (tau + dt);
                if (loop % 4 == 3)
                {
                    command.Add(u);
                    duty.Add(e);
                }
            }
            long commandUs = Half(command, 750 - 1000), dutyUs = Half(duty, 0);
            smoothingUs = commandUs;
            escUs = dutyUs - commandUs;
        }

        static double Gain(double hz, double dt)
        {
            double w = 2 * Math.PI * hz * dt;
            return w / (w + 1);
        }

        // Sample m stands at m ms + atUs from the frame; the first crossing of 0.5, linear between samples.
        static long Half(List<double> v, long atUs)
        {
            for (int m = 1; m < v.Count; m++)
                if (v[m] >= 0.5)
                    return (m - 1) * 1000 + atUs + (long)Math.Round((0.5 - v[m - 1]) / (v[m] - v[m - 1]) * 1000);
            Assert.Fail("never halfway");
            return 0;
        }

        static string Spread(in LatencySpread s) =>
            "p50 " + Ms(s.P50Us) + ", p99 " + Ms(s.P99Us) + " ms (min " + Ms(s.MinUs) + ", max " + Ms(s.MaxUs) + ", mean " + (s.MeanUs / 1000).ToString("0.000", C) + ")";

        static string Ms(long us) => (us / 1000.0).ToString("0.000", C);

        static List<long> Sorted(HashSet<long> set)
        {
            var list = new List<long>(set);
            list.Sort();
            return list;
        }

        // A repository file, found up from this file's own path (the .NET tools compile it in place) or from Unity's
        // project folder (unity/, the editor's working directory).
        static string RepositoryFile(string relative, [CallerFilePath] string here = "")
        {
            var dirs = new List<string> { Directory.GetCurrentDirectory() };
            if (!string.IsNullOrEmpty(here)) dirs.Insert(0, Path.GetDirectoryName(Path.GetFullPath(here)));
            foreach (string start in dirs)
                for (DirectoryInfo d = new DirectoryInfo(start); d != null; d = d.Parent)
                    if (File.Exists(Path.Combine(d.FullName, relative)))
                        return Path.Combine(d.FullName, relative);
            Assert.Fail(relative + " was not found up from " + here);
            return null;
        }
    }
}
