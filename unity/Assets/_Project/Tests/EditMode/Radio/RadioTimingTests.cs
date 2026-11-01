using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Fpv450.Input.Radio;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using NUnit.Framework;

namespace Fpv450.Tests.Radio
{
    // tx-input (p6-input-app 1.1, INP-04…06): "Render rate does not touch the sticks" with a recorded TX12 stream at
    // render caps of 30, 60 and 240 fps on the real input and flight threads; "Pulling the cable mid-flight" through
    // the flight log; the report-interval histogram; the raw map, a recording's round trip and the keyboard fallback.
    public sealed class RadioTimingTests
    {
        const long LostAtUs = 1234567;

        // ---- Render rate does not touch the sticks ----

        const long FlightStartUs = 100000; // the recording's µs of step 0
        const int Steps = 1200;            // compared: to 1.299 s, past the loss at 1.2346 s

        // The recording played in real time into the input thread (RadioReader), the flight on its thread, and this
        // thread a renderer at fps: each frame reads the live display and the interpolated pose, then works to its cap.
        // The control (frameBound) is the design the contract forbids: reports taken in by the frame, as Update would.
        static StickSample[] FlyThreaded(int fps, bool frameBound, out string notes)
        {
            RecordedRadio radio = Tx12Stream.Make(0, 1400000, LostAtUs);
            radio.LeadUs = 10000; // handed over 10 ms early so the stamps, not the threads, decide (RecordedRadio)
            var clock = new ShiftedClock(50000);
            radio.Clock = clock;
            var inbox = new StickInbox();
            var sticks = new SticksInForce(Steps + 2000);
            var poses = new PoseBuffer();
            var scheduler = new FlightScheduler(new FlightCore(Flights.FreeBody()), inbox, poses, clock, sticks);

            RadioReader reader = null;
            Thread pump = null;
            var queue = new ConcurrentQueue<StickSample>();
            bool stop = false;
            if (frameBound)
            {
                pump = new Thread(() =>
                {
                    var map = new RawStickMap();
                    var r = new RadioReport(radio.Axes.Length);
                    while (!Volatile.Read(ref stop))
                    {
                        RadioRead got = radio.Read(r, 20);
                        if (got == RadioRead.Report)
                        {
                            map.Map(r, radio.Axes, out StickSample s);
                            queue.Enqueue(s);
                        }
                        else if (got == RadioRead.Lost)
                        {
                            queue.Enqueue(new StickSample { HostUs = r.HostUs, Failsafe = true });
                            return;
                        }
                    }
                }) { IsBackground = true, Priority = ThreadPriority.Highest };
                pump.Start();
            }
            else
            {
                reader = new RadioReader(Flights.Once(radio)) { Inbox = inbox };
                reader.Start();
            }

            scheduler.Start(FlightStartUs);
            var display = new RadioReport(radio.Axes.Length);
            long frameUs = 1000000 / fps, frames = 0;
            try
            {
                while (true)
                {
                    long frameStart = clock.NowUs();
                    if (frameStart >= FlightStartUs + (Steps + 50) * FlightCore.StepUs) break;
                    if (frameBound)
                        while (queue.TryDequeue(out StickSample s))
                            inbox.TryWrite(s);
                    else
                        reader.ReadLatest(display);
                    ref readonly PosePair pair = ref poses.Read();
                    pair.Interpolate(clock.NowUs(), out PoseFrame _);
                    frames++;
                    while (clock.NowUs() < frameStart + frameUs) Thread.SpinWait(50);
                }
            }
            finally
            {
                scheduler.Stop();
                reader?.Stop();
                Volatile.Write(ref stop, true);
                pump?.Join();
            }
            Assert.That(scheduler.Fault, Is.Null);
            Assert.That(sticks.Count, Is.GreaterThanOrEqualTo(Steps), "steps flown");
            notes = fps + " fps" + (frameBound ? ", frame-bound control" : "") + ": " + frames + " frames, " + sticks.Count + " steps, "
                + scheduler.HitchCount + " hitches, " + inbox.Dropped + " dropped"
                + (reader != null ? ", " + reader.Reports + " reports and " + reader.Losses + " loss on the input thread" : "");
            var result = new StickSample[Steps];
            Array.Copy(sticks.Sticks, result, Steps);
            return result;
        }

        static int FirstDifference(StickSample[] a, StickSample[] b)
        {
            for (int k = 0; k < a.Length; k++)
                if (!a[k].Equals(b[k]))
                    return k;
            return -1;
        }

        [Test]
        public void RenderRateDoesNotTouchTheSticks()
        {
            StickSample[] expected = Flights.InForce(Flights.Expected(Tx12Stream.Make(0, 1400000, LostAtUs)), FlightStartUs, Steps);
            int failsafeStep = (int)((LostAtUs - FlightStartUs + FlightCore.StepUs - 1) / FlightCore.StepUs);
            Assert.That(expected[failsafeStep].Failsafe && !expected[failsafeStep].Arm && expected[failsafeStep - 1].Arm, "the expectation itself");

            foreach (int fps in new[] { 30, 60, 240 })
            {
                StickSample[] got = FlyThreaded(fps, false, out string notes);
                TestContext.WriteLine(notes);
                Assert.That(FirstDifference(expected, got), Is.EqualTo(-1), fps + " fps: the first step whose stick in force differs");
                Assert.That(got[failsafeStep].Failsafe && got[failsafeStep].HostUs == LostAtUs, fps + " fps: the failsafe's step");
            }

            // The control shows the test can fail: reports taken in once a frame miss the steps they belong to at 30 fps.
            StickSample[] bound = FlyThreaded(30, true, out string control);
            int first = FirstDifference(expected, bound);
            int stale = 0;
            for (int k = 0; k < Steps; k++)
                if (!expected[k].Equals(bound[k]))
                    stale++;
            TestContext.WriteLine(control + "; " + stale + " of " + Steps + " steps had a stale stick, the first at step " + first);
            Assert.That(first, Is.GreaterThanOrEqualTo(0), "the frame-bound control must differ");
        }

        // ---- Pulling the cable mid-flight ----

        [Test]
        public void PullingTheCableMidFlight()
        {
            const long startUs = 500000;
            var clock = new VirtualClock();
            RecordedRadio radio = Tx12Stream.Make(0, 2000000, LostAtUs);
            radio.Clock = clock;
            var reader = new RadioReader(Flights.Once(radio));

            // 0.5 s on the setup screen: the input thread reads, no flight takes the sticks yet.
            for (long t = 0; t < startUs; t += 100)
            {
                clock.Now = t;
                while (reader.Pump(0)) { }
            }
            var inbox = new StickInbox();
            reader.Inbox = inbox;
            FlightSetup setup = Flights.FreeBody();
            var core = new FlightCore(setup);
            LogHeader header = LogHeader.For(setup, core);
            header.Build = "inputtests";
            header.WorldId = "floor";
            header.Input = reader.Source;
            header.InputDevice = reader.DeviceName;
            header.InputIntervals = reader.Intervals(10000000);
            var log = new MemoryStream();
            var recorder = new FlightRecorder(log, header, FlightRecorder.MinCapacity << 6, false);
            var scheduler = new FlightScheduler(core, inbox, new PoseBuffer(), clock, recorder);
            scheduler.Begin(startUs);
            for (long t = startUs; t <= 2000000; t += 100)
            {
                clock.Now = t;
                while (reader.Pump(0)) { }
                scheduler.RunDue(t);
            }
            recorder.Close(inbox.Dropped);
            Assert.That(recorder.Fault, Is.Null);
            Assert.That((reader.Losses, reader.Connected), Is.EqualTo((1L, false)));

            byte[] bytes = log.ToArray();
            var steps = new List<LoggedStep>();
            using (var r = new FlightLogReader(new MemoryStream(bytes)))
                while (r.Next())
                    if (r.Kind == LogFormat.StepBlock)
                    {
                        r.ReadStep(out LoggedStep s);
                        steps.Add(s);
                    }

            // The step whose host time is the first at or after the loss disarms: steps[k] is step k's record.
            int k0 = (int)((LostAtUs - startUs + FlightCore.StepUs - 1) / FlightCore.StepUs);
            long stepUs = scheduler.StepHostUs(k0);
            Assert.That(stepUs - LostAtUs, Is.InRange(0, FlightCore.StepUs - 1), "within one step of the loss");
            Assert.That(steps[k0 - 1].State.Armed && !steps[k0 - 1].Stick.Failsafe, "armed the step before");
            Assert.That(!steps[k0].State.Armed && steps[k0].Stick.Failsafe && steps[k0].Stick.HostUs == LostAtUs, "the failsafe's step");
            for (int k = k0; k < steps.Count; k++)
                Assert.That(!steps[k].State.Armed && steps[k].Stick.Failsafe, "disarmed after, step " + k);

            FlightSummary summary = FlightSummary.Read(new MemoryStream(bytes));
            Assert.That(summary.Complete);
            Assert.That(summary.Failsafes.Count, Is.EqualTo(1));
            Assert.That((summary.Failsafes[0].State.Step, summary.Failsafes[0].Stick.HostUs), Is.EqualTo((k0 + 1L, LostAtUs)));
            Assert.That(summary.Header.Input, Is.EqualTo(InputSource.Radio));
            var text = new StringWriter();
            summary.Print(text);
            TestContext.WriteLine(text.ToString());
            StringAssert.Contains("FAILSAFE: the radio was lost at host " + LostAtUs + " us; step " + (k0 + 1), text.ToString());
            StringAssert.Contains("input: radio 'RadioMaster TX12 (synthetic)'; report interval over 0.5 s before the flight", text.ToString());

            ReplayResult replay = FlightReplay.Run(new MemoryStream(bytes), id => id == "floor" ? FlightWorld.Floor() : null, 0);
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
        }

        // ---- The report-interval histogram ----

        [Test]
        public void TheReportIntervalHistogramOfATx12()
        {
            var clock = new VirtualClock();
            RecordedRadio radio = Tx12Stream.Make(0, 12000000);
            radio.Clock = clock;
            var reader = new RadioReader(Flights.Once(radio));
            for (long t = 0; t < 12000000; t += 100)
            {
                clock.Now = t;
                while (reader.Pump(0)) { }
            }
            Assert.That(reader.Reports, Is.EqualTo(radio.Count));

            ReportIntervals r = reader.Intervals(10000000);
            TestContext.WriteLine("last 10 s: " + r.Count + " intervals, mean " + (double)r.SumUs / r.Count + " us, min " + r.MinUs + ", p50 "
                + r.PercentileUs(50) + ", p99 " + r.PercentileUs(99) + ", max " + r.MaxUs);
            Assert.That(r.Count, Is.InRange(4998, 5000), "10 s of 2 ms reports");
            Assert.That(r.SumUs, Is.LessThanOrEqualTo(10000000));
            Assert.That((double)r.SumUs / r.Count, Is.EqualTo(2000).Within(0.5), "the radio's 2 ms mixer on the host's 1 ms frames");
            Assert.That(r.MinUs, Is.GreaterThanOrEqualTo(1820));
            Assert.That(r.MaxUs, Is.LessThanOrEqualTo(2180), "the wake jitter of 20–200 us either side");
            Assert.That(r.PercentileUs(50), Is.InRange(1900, 2200));
            long binned = 0;
            foreach (long n in r.Counts) binned += n;
            Assert.That(binned, Is.EqualTo(r.Count));

            Assert.That(reader.Intervals(100000).Count, Is.InRange(49, 50), "a 0.1 s window");
        }

        // ---- The raw map, a recording's round trip, the keyboard ----

        [Test]
        public void TheRawMapIsAetrWithTheArmOnChannelFive()
        {
            var map = new RawStickMap();
            var r = new RadioReport(8) { HostUs = 42 };
            int[] extremes = { 0, 2047, 0, 2047, 2047, 1024, 1024, 1024 };
            Array.Copy(extremes, r.Axes, 8);
            map.Map(r, Tx12Stream.Axes, out StickSample s);
            Assert.That((s.HostUs, s.Roll, s.Pitch, s.Throttle, s.Yaw, s.Arm, s.Failsafe), Is.EqualTo((42L, -1.0, 1.0, 0.0, 1.0, true, false)));

            for (int i = 0; i < 8; i++) r.Axes[i] = 1024; // EdgeTX's centre: channel 0 + 1024
            map.Map(r, Tx12Stream.Axes, out s);
            Assert.That(s.Roll, Is.EqualTo(1.0 / 2047).Within(1e-12), "half a step off the range's middle until calibration");
            Assert.That(s.Arm, Is.True, "ch5 centred is above the middle by that half step");
            r.Axes[4] = 0;
            map.Map(r, Tx12Stream.Axes, out s);
            Assert.That(s.Arm, Is.False);

            // A gamepad of four axes arms on its first button.
            var pad = new[] { new RadioAxis(1, 0x30, -32768, 32767), new RadioAxis(1, 0x31, -32768, 32767), new RadioAxis(1, 0x32, 0, 255), new RadioAxis(1, 0x35, 0, 255) };
            var p = new RadioReport(4) { Buttons = 1 };
            p.Axes[0] = -32768;
            p.Axes[2] = 255;
            map.Map(p, pad, out s);
            Assert.That((s.Roll, s.Throttle, s.Arm), Is.EqualTo((-1.0, 1.0, true)));
        }

        [Test]
        public void ARecordingRoundTripsAsText()
        {
            RecordedRadio a = Tx12Stream.Make(0, 100000, 60000);
            var text = new StringWriter();
            a.Save(text);
            RecordedRadio b = RecordedRadio.Load(new StringReader(text.ToString()));
            Assert.That((b.Name, b.Source, b.ButtonCount, b.LostAtUs, b.Count), Is.EqualTo((a.Name, a.Source, a.ButtonCount, a.LostAtUs, a.Count)));
            Assert.That(b.Axes, Is.EqualTo(a.Axes));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b.TimeOf(i), Is.EqualTo(a.TimeOf(i)));
                Assert.That(b.AxesOf(i), Is.EqualTo(a.AxesOf(i)));
                Assert.That(b.ButtonsOf(i), Is.EqualTo(a.ButtonsOf(i)));
            }
        }

        [Test]
        public void TheKeyboardFallbackHoldsAndToggles()
        {
            var k = new KeyboardSticks();
            k.Update(KeyboardKeys.None, 0, out StickSample s);
            Assert.That((s.Throttle, s.Arm, s.Failsafe), Is.EqualTo((0.0, false, false)));
            for (long t = 10000; t <= 1000000; t += 10000) k.Update(KeyboardKeys.ThrottleUp | KeyboardKeys.ArmToggle, t, out s);
            Assert.That(s.Throttle, Is.EqualTo(0.5).Within(1e-9), "W held 1 s at 0.5 a second");
            Assert.That(s.Arm, Is.True, "toggled once on the press, not on every frame it is held");
            k.Update(KeyboardKeys.RollLeft | KeyboardKeys.PitchForward | KeyboardKeys.YawRight, 1010000, out s);
            Assert.That((s.Roll, s.Pitch, s.Yaw, s.Arm), Is.EqualTo((-0.5, 0.5, 0.5, true)));
            Assert.That(s.Throttle, Is.EqualTo(0.5).Within(1e-9), "the throttle stays where it is left");
            k.Update(KeyboardKeys.ArmToggle, 1020000, out s);
            Assert.That(s.Arm, Is.False);
            k.Update(KeyboardKeys.ThrottleDown, 4020000, out s);
            Assert.That(s.Throttle, Is.EqualTo(0.0), "clamped at the bottom");
        }
    }
}
