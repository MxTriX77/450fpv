using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // physics-log (LOG-01, LOG-02, LOG-05): "Replay matches the flight" and "A truncated log still reads" on the
    // bootstrap drone with its flight controller and propulsion (1.4), its drag in the Windy wind (1.5; the field, p7c-wind 1.4),
    // launched into the air and flown by a scripted pilot, through the scheduler, the ring and the flush worker; the refusals, a divergence named by step and field,
    // a full ring that waits and logs a hitch, the header's round trip, and the guards that keep the record and the
    // header covering the state and the setup as later tasks extend them. p7h-step-budget 1.1: the step times in the end
    // block, their 0 B, and a format-69 log still read.
    public sealed class FlightLogTests
    {
        const long StartUs = 1000000;
        const long TickUs = 250;
        const long StallAtUs = StartUs + 700000, StallUs = 30000;

        sealed class VirtualClock : IHostClock
        {
            public long Now;
            public long NowUs() => Now;
        }

        static FlightWorld OpenWorld(string id) => id == "floor" ? FlightWorld.Floor() : null;

        static FlightSetup Setup(DroneSpec spec) => new FlightSetup
        {
            Seed = 0x450F,
            World = FlightWorld.Floor(),
            Mass = spec.BuildMass(spec.Nominal),
            Propulsion = spec.BuildPropulsion(),
            Aero = spec.BuildAero(),
            Wind = WindSetup.Of(WindLevel.Windy),
            StartPosition = new Double3(0, 100, 0),
            StartVelocity = new Double3(3, 20, -1),
            StartBodyRate = new Double3(2, -1, 0.5),
            StartPackCharge = 0.8,
        };

        static LogHeader Header(FlightSetup setup, FlightCore core, DroneSpec spec)
        {
            LogHeader h = LogHeader.For(setup, core);
            h.Build = "simtests";
            h.Isa = "test";
            h.WorldId = "floor";
            h.SetDrone(spec, spec.Nominal);
            return h;
        }

        // A ~250 Hz report stream with jitter, armed from the 50th report with the throttle low (Betaflight's rule), every
        // axis moving from the 60th.
        static StickSample Report(int i, long hostUs) => new StickSample
        {
            HostUs = hostUs,
            Roll = (i * 37 % 1001 - 500) / 500.0,
            Pitch = (i * 53 % 1001 - 500) / 500.0,
            Yaw = (i * 71 % 1001 - 500) / 500.0,
            Throttle = i < 60 ? 0 : i * 29 % 1001 / 1000.0,
            Arm = i >= 50,
        };

        // Flies the scripted flight for `steps` steps through the scheduler on a virtual clock, with one 30 ms process
        // stall at 0.7 s (a hitch), into a recorder on stream; returns its header.
        static LogHeader Record(Stream stream, long steps, Action<LogHeader> edit = null, int capacity = FlightRecorder.DefaultCapacity)
        {
            DroneSpec spec = BootstrapDrone.Spec();
            FlightSetup setup = Setup(spec);
            var core = new FlightCore(setup);
            LogHeader header = Header(setup, core, spec);
            edit?.Invoke(header);
            var recorder = new FlightRecorder(stream, header, capacity, ownsStream: false);
            var clock = new VirtualClock { Now = StartUs };
            var inbox = new StickInbox();
            var scheduler = new FlightScheduler(core, inbox, new PoseBuffer(), clock, recorder);
            scheduler.Begin(StartUs);
            long report = StartUs - 2000;
            for (int i = 0, t = 0; core.State.Step < steps; t++)
            {
                long now = StartUs + t * TickUs;
                clock.Now = now;
                for (; report <= now; i++)
                {
                    inbox.TryWrite(Report(i, report));
                    report += 3700 + i * 131 % 600;
                }
                if (!(now >= StallAtUs && now < StallAtUs + StallUs)) scheduler.RunDue(now);
            }
            recorder.Close(inbox.Dropped);
            Assert.That(recorder.Fault, Is.Null);
            return header;
        }

        // The byte offset of step's record block in a log.
        static long BlockOf(byte[] log, long step)
        {
            using (var reader = new FlightLogReader(new MemoryStream(log)))
            {
                while (reader.Next())
                    if (reader.Kind == LogFormat.StepBlock && LogFormat.StepOf(reader.Payload) == step)
                        return reader.Position;
            }
            Assert.Fail("no record of step " + step);
            return -1;
        }

        static byte[] Head(byte[] log, long length)
        {
            var cut = new byte[length];
            Array.Copy(log, cut, length);
            return cut;
        }

        [Test]
        public void ReplayMatchesTheFlight()
        {
            // 120 s of 1300 B steps: 156 MB, kept in memory.
            var log = new MemoryStream();
            Record(log, 120000);
            DroneSpec spec = BootstrapDrone.Spec();
            Assert.That(log.Length, Is.GreaterThan(120000L * (LogFormat.BlockHeaderSize + LogFormat.StepSize)));

            log.Position = 0;
            ReplayResult r = FlightReplay.Run(log, OpenWorld, spec.ContentHash);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Match), r.Message);
            Assert.That(r.Steps, Is.EqualTo(120000), "every step record replayed");

            log.Position = 0;
            FlightSummary summary = FlightSummary.Read(log);
            Assert.That(summary.Complete, Is.True);
            Assert.That(summary.Gaps, Is.EqualTo(0));
            // The 30 ms stall, past the 20 ms cap (p7h-step-budget 2.1): its Drop of 10 ms, then the Late pass of the 21 steps
            // left due; the virtual clock runs unpaced, so a slow flush worker (Unity's Mono) may also fill the ring, and each
            // wait for it is a LogFull hitch with nothing dropped. The replay above matched through the drop.
            Assert.That(summary.Events.FindAll(e => e.Event.Kind == EventKind.Hitch && e.Event.Index == (byte)HitchCause.Late)
                .ConvertAll(e => (e.Step, e.Event.A, e.Event.B)), Is.EqualTo(new[] { (701L, 21.0, 20000.0) }), "the stall's catch-up, capped");
            Assert.That(summary.Events.FindAll(e => e.Event.Kind == EventKind.Hitch && e.Event.Index == (byte)HitchCause.Drop)
                .ConvertAll(e => (e.Step, e.Event.A, e.Event.B)), Is.EqualTo(new[] { (701L, 30000.0, 10000.0) }), "the stall's drop");
            Assert.That(summary.Count(EventKind.Hitch), Is.EqualTo(2 + summary.End.LogWaits), "every other hitch is a wait for the ring");
            Assert.That(summary.End.Steps, Is.EqualTo(120000));
            Assert.That(summary.Last.State.Armed, Is.True, "the flight armed and flew");
            Assert.That(summary.Last.State.RotorSpeed.RearRight, Is.GreaterThan(100), "the motors turn");
            Assert.That(summary.Last.State.PackCharge0, Is.LessThan(0.8), "the packs drained");
            Assert.That(summary.Count(EventKind.MixerSaturation), Is.GreaterThan(0), "the random sticks saturate the mixer");

            // One motor 1 g heavier: a valid drone.json of another hash, refused before the first step.
            string text = BootstrapDrone.Text();
            const string motor = "\"mass_kg\": {\"value\": 0.130";
            int at = text.IndexOf(motor, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0));
            string changed = text.Substring(0, at) + motor + "1" + text.Substring(at + motor.Length);
            ulong changedHash = DroneSpec.Parse(changed).ContentHash;
            Assert.That(changedHash, Is.Not.EqualTo(spec.ContentHash));

            log.Position = 0;
            ReplayResult refused = FlightReplay.Run(log, OpenWorld, changedHash);
            Assert.That(refused.Outcome, Is.EqualTo(ReplayOutcome.Refused), refused.Message);
            Assert.That(refused.Steps, Is.EqualTo(0));
            StringAssert.Contains("drone.json differs", refused.Message);
            StringAssert.Contains(spec.ContentHash.ToString("X16"), refused.Message, "names the log's hash");
            StringAssert.Contains(changedHash.ToString("X16"), refused.Message, "names the replay's hash");
        }

        // PLT-04, PHB-01 (task 1.7): the flight thread's timing changes only the logged hitches. The repository's drone with
        // its contacts, the Windy wind and the builder's chain (GoldenFlights' powered setup) is flown 3 s on the jittered
        // stick stream by three workers on a virtual clock: one exactly on each step's time, one forced early (it wakes
        // 10–400 µs apart, mostly before its step is due), one forced late (2–4000 µs apart, and once the 20 ms cap, so
        // passes catch up 2–20 steps; p7h-step-budget 2.1). Every step record and every event but the hitches is the same,
        // byte for byte: within the cap, timing changes nothing.
        enum Worker { OnTime, Early, Late }

        const long WorkerSteps = 3000;

        static byte[] FlyWorker(Worker worker, out long passes, out long idle, out long hitches)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = GoldenFlights.PoweredSetup(spec);
            var core = new FlightCore(setup);
            var log = new MemoryStream();
            var recorder = new FlightRecorder(log, Header(setup, core, spec), FlightRecorder.DefaultCapacity, ownsStream: false);
            var clock = new VirtualClock { Now = StartUs };
            var inbox = new StickInbox();
            var scheduler = new FlightScheduler(core, inbox, new PoseBuffer(), clock, recorder);
            scheduler.Begin(StartUs);
            ulong lcg = 0x9E3779B97F4A7C15UL;
            long report = StartUs - 2000, wake = StartUs;
            passes = idle = 0;
            for (int i = 0; core.State.Step < WorkerSteps;)
            {
                clock.Now = wake;
                for (; report <= wake; i++)
                {
                    inbox.TryWrite(Report(i, report));
                    report += 3700 + i * 131 % 600;
                }
                passes++;
                if (scheduler.RunDue(wake) == 0) idle++;
                lcg = unchecked(lcg * 6364136223846793005UL + 1442695040888963407UL);
                long draw = (long)(lcg >> 33);
                if (worker == Worker.OnTime) wake = scheduler.StepHostUs(core.State.Step);
                else if (worker == Worker.Early) wake += 10 + draw % 391;
                else wake += core.State.Step == 1500 ? FlightScheduler.MaxOwedUs : 2 + draw % 3999;
            }
            recorder.Close(inbox.Dropped);
            Assert.That(recorder.Fault, Is.Null);
            hitches = scheduler.HitchCount;
            return log.ToArray();
        }

        // Every step record and every event but the hitches up to step `last`, in order (a late worker's last pass may run
        // past it); the Late hitches counted apart.
        static List<byte[]> Deterministic(byte[] log, long last, out int late)
        {
            var blocks = new List<byte[]>();
            late = 0;
            using (var reader = new FlightLogReader(new MemoryStream(log)))
            {
                while (reader.Next())
                {
                    if (reader.Kind == LogFormat.EventBlock)
                    {
                        reader.ReadEvent(out LoggedEvent e);
                        if (e.Event.Kind == EventKind.Hitch)
                        {
                            if (e.Event.Index == (byte)HitchCause.Late) late++;
                            continue;
                        }
                        if (e.Step > last) continue;
                    }
                    if (reader.Kind == LogFormat.StepBlock && LogFormat.StepOf(reader.Payload) > last) continue;
                    if (reader.Kind == LogFormat.StepBlock || reader.Kind == LogFormat.EventBlock) blocks.Add(reader.Payload.ToArray());
                }
                Assert.That(reader.Complete, Is.True);
            }
            return blocks;
        }

        [Test]
        public void AWorkerEarlyOrLateChangesOnlyTheHitches()
        {
            List<byte[]> reference = Deterministic(FlyWorker(Worker.OnTime, out _, out long refIdle, out long refHitches), WorkerSteps, out int refLate);
            Assert.That(refHitches, Is.EqualTo(0));
            Assert.That(refLate, Is.EqualTo(0));
            Assert.That(refIdle, Is.EqualTo(0), "on time, every pass runs its one step");
            foreach (Worker w in new[] { Worker.Early, Worker.Late })
            {
                List<byte[]> blocks = Deterministic(FlyWorker(w, out long passes, out long idle, out long hitches), WorkerSteps, out int late);
                TestContext.WriteLine($"{w}: {passes} passes, {idle} with nothing due, {hitches} hitches, {blocks.Count} records and events (on time: {reference.Count})");
                Assert.That(blocks.Count, Is.EqualTo(reference.Count), w + ": the records and events");
                for (int k = 0; k < blocks.Count; k++)
                    Assert.That(blocks[k], Is.EqualTo(reference[k]), w + ": block " + k);
                Assert.That(late, Is.EqualTo(hitches), w + ": each late pass is one logged hitch");
                if (w == Worker.Early)
                {
                    Assert.That(idle, Is.GreaterThan(passes / 2), "most early passes find nothing due");
                    Assert.That(hitches, Is.EqualTo(0));
                }
                else
                {
                    Assert.That(hitches, Is.GreaterThan(100), "late passes catch up and log it");
                }
            }
        }

        // p7h-step-budget 1.1, "A flight reports its step times": a host clock on which only the steps take time (its time is
        // the cost of the steps the core has run), so step k's Advance takes StepCost(k) µs whoever reads the clock, and when.
        sealed class StepCostClock : IHostClock
        {
            public FlightCore Core;
            public Func<long, long> Cost;
            long _steps, _now;

            public long NowUs()
            {
                for (; _steps < Core.State.Step; _steps++) _now += Cost(_steps);
                return _now;
            }
        }

        const int TimedSteps = 3000;

        // 2960 steps of 240 µs, 30 of 900, 7 of 1500 and 3 of 12000 (past the bins' 10 ms): p50 240, p99 900, p99.9 1500 at
        // its rank's very step, max 12000, and 10 over 1 ms.
        static long StepCost(long k) => k % 1000 == 999 ? 12000 : k % 400 == 123 && k < 2800 ? 1500 : k % 100 == 50 ? 900 : 240;

        // The scripted flight's first 3 s through the scheduler, each pass at its step's time, on the step-cost clock. The
        // ring holds the whole flight, so no LogFull wait depends on the flush worker.
        static byte[] FlyTimed(Func<long, long> cost, out StepTimeSummary times)
        {
            DroneSpec spec = BootstrapDrone.Spec();
            FlightSetup setup = Setup(spec);
            var core = new FlightCore(setup);
            var log = new MemoryStream();
            var recorder = new FlightRecorder(log, Header(setup, core, spec), 1 << 24, ownsStream: false);
            var inbox = new StickInbox();
            var scheduler = new FlightScheduler(core, inbox, new PoseBuffer(), new StepCostClock { Core = core, Cost = cost }, recorder);
            scheduler.Begin(StartUs);
            for (int k = 0; k < TimedSteps; k++)
            {
                long due = scheduler.StepHostUs(k);
                if (k % 4 == 0) inbox.TryWrite(Report(k / 4, due));
                scheduler.RunDue(due);
            }
            times = scheduler.Times.Summary();
            recorder.Close(inbox.Dropped, times);
            Assert.That(recorder.Fault, Is.Null);
            return log.ToArray();
        }

        [Test]
        public void AFlightReportsItsStepTimes()
        {
            byte[] log = FlyTimed(StepCost, out StepTimeSummary times);
            var costs = new long[TimedSteps];
            for (int k = 0; k < TimedSteps; k++) costs[k] = StepCost(k);
            Array.Sort(costs);
            long Rank(int permille) => costs[(TimedSteps * permille + 999) / 1000 - 1];
            Assert.That((times.Count, times.Overruns, times.P50Us, times.P99Us, times.P999Us, times.MaxUs),
                Is.EqualTo((3000L, 10L, Rank(500), Rank(990), Rank(999), costs[TimedSteps - 1])), "nearest rank of the sorted costs");
            Assert.That((times.P50Us, times.P99Us, times.P999Us, times.MaxUs), Is.EqualTo((240L, 900L, 1500L, 12000L)));

            FlightSummary s = FlightSummary.Read(new MemoryStream(log));
            Assert.That(s.Complete, s.CutReason);
            Assert.That(s.End.Times, Is.EqualTo(times), "the end record carries them");
            var text = new StringWriter();
            s.Print(text);
            StringAssert.Contains("format " + LogFormat.Version + ", model " + LogFormat.ModelVersion, text.ToString());
            StringAssert.Contains("step times: 3000 steps, 10 over 1 ms, p50 240 us, p99 900 us, p99.9 1500 us, max 12000 us", text.ToString());

            ReplayResult r = FlightReplay.Run(new MemoryStream(log), OpenWorld, BootstrapDrone.Spec().ContentHash);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Match), r.Message);
            Assert.That(r.Steps, Is.EqualTo(TimedSteps));

            // Timing never feeds the state: every step free, the header, the records, the events, the end's counts and its
            // steps timed are the same bytes; the first that differs is the end's overruns.
            byte[] free = FlyTimed(k => 0, out StepTimeSummary none);
            Assert.That((none.Count, none.Overruns, none.MaxUs), Is.EqualTo((3000L, 0L, 0L)));
            Assert.That(free.Length, Is.EqualTo(log.Length));
            int overruns = Array.Find(LogFormat.EndFields, f => f.Name == "step_overruns").Offset;
            Assert.That(FirstDifference(free, log), Is.EqualTo(log.Length - LogFormat.EndSize + overruns));
        }

        // The first byte where a and b differ, or the shorter's length.
        static int FirstDifference(byte[] a, byte[] b)
        {
            int n = Math.Min(a.Length, b.Length), i = 0;
            while (i < n && a[i] == b[i]) i++;
            return i;
        }

        // p7h-step-budget 1.1, "The timer costs nothing in steady state": 60 s of the scripted flight through the scheduler, its
        // step timer on the process's own clock (HostClock), into the recorder's ring and flush worker; after the first step
        // the flight thread allocates 0 B.
        [Test]
        public void TheTimerCostsNothingInSteadyState()
        {
            const long steps = 60000;
            DroneSpec spec = BootstrapDrone.Spec();
            FlightSetup setup = Setup(spec);
            var core = new FlightCore(setup);
            var recorder = new FlightRecorder(Stream.Null, Header(setup, core, spec), FlightRecorder.DefaultCapacity, ownsStream: false);
            var inbox = new StickInbox();
            var scheduler = new FlightScheduler(core, inbox, new PoseBuffer(), HostClock.Instance, recorder);
            scheduler.Begin(StartUs);
            long allocated = 0;
            for (long k = 0; k < steps; k++)
            {
                long due = scheduler.StepHostUs(k);
                long before = GC.GetAllocatedBytesForCurrentThread();
                if (k % 4 == 0) inbox.TryWrite(Report((int)(k / 4), due));
                scheduler.RunDue(due);
                if (k >= 1) allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }
            StepTimeSummary times = scheduler.Times.Summary();
            recorder.Close(inbox.Dropped, times);
            TestContext.WriteLine("60 s on this host: " + times);
            Assert.That(recorder.Fault, Is.Null);
            Assert.That(allocated, Is.Zero, "0 B a step after the first, the timer on");
            Assert.That(times.Count, Is.EqualTo(steps));
            Assert.That(times.MaxUs, Is.GreaterThan(0), "timed on the host's clock");
        }

        // p7h-step-budget 2.1, "A late flight never builds growing lag": a host clock on which steps 1000–1499 cost 3 ms each
        // and every other step 200 µs, and which, like the input thread, puts each report (every 500 µs) in the inbox once
        // its time has come. The driver is the flight thread's loop (FlightScheduler.Run): a pass at the clock's now, then
        // a sleep to the next step's host time.
        sealed class SlowClock : IHostClock
        {
            public const long ReportUs = 500;
            public FlightCore Core;
            public StickInbox Inbox;
            long _steps, _now = StartUs, _report = StartUs - 2000;
            int _reports;

            public static long Cost(long k) => k >= 1000 && k < 1500 ? 3000 : 200;

            public long NowUs()
            {
                for (; _steps < Core.State.Step; _steps++) _now += Cost(_steps);
                for (; _report <= _now; _reports++, _report += ReportUs) Inbox.TryWrite(Report(_reports, _report));
                return _now;
            }

            public void SleepUntil(long us)
            {
                if (NowUs() < us) _now = us;
            }
        }

        const int SlowSteps = 3000;

        // Each step's start on the slow clock and the stamp of the stick it read, beside the log; the scheduler and its poses.
        sealed class StepWatch : IFlightRecorder
        {
            public IFlightRecorder Inner;
            public SlowClock Clock;
            public FlightScheduler Scheduler;
            public PoseBuffer Poses;
            public readonly long[] StartUs = new long[SlowSteps], StickUs = new long[SlowSteps];

            public void RecordStep(in FlightState state, in StickSample stick, in StepTrace trace, StepEvents events)
            {
                long k = state.Step - 1;
                StartUs[k] = Clock.NowUs() - SlowClock.Cost(k); // nothing but the step ran since it started
                StickUs[k] = stick.HostUs;
                Inner.RecordStep(state, stick, trace, events);
            }

            public void RecordHitch(in Hitch hitch) => Inner.RecordHitch(hitch);
        }

        static byte[] FlySlow(out StepWatch watch, out long allocated)
        {
            DroneSpec spec = BootstrapDrone.Spec();
            FlightSetup setup = Setup(spec);
            var core = new FlightCore(setup);
            var log = new MemoryStream();
            var recorder = new FlightRecorder(log, Header(setup, core, spec), 1 << 24, ownsStream: false);
            var inbox = new StickInbox();
            var clock = new SlowClock { Core = core, Inbox = inbox };
            watch = new StepWatch { Inner = recorder, Clock = clock, Poses = new PoseBuffer() };
            var scheduler = watch.Scheduler = new FlightScheduler(core, inbox, watch.Poses, clock, watch);
            scheduler.Begin(StartUs);
            allocated = 0;
            while (core.State.Step < SlowSteps)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                bool first = core.State.Step == 0;
                scheduler.RunDue(clock.NowUs());
                if (!first) allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                clock.SleepUntil(scheduler.StepHostUs(core.State.Step));
            }
            recorder.Close(inbox.Dropped, scheduler.Times.Summary());
            Assert.That(recorder.Fault, Is.Null);
            return log.ToArray();
        }

        // "A slow stretch costs one bounded hiccup": 500 steps of 3 ms. Each step's host time, rebuilt from the log alone
        // (step 0's, k ms, and the B of every drop up to it, LogHeader.StartHostUs), is never more than the cap before the
        // step started, and the stick it read is the one in force then. The stretch adds 2 ms of lag a step (3 ms run, 1 ms
        // of flight): the first 10 steps build it to the cap, then each of the 490 that start past it drops its 2 ms, 980 ms
        // in all, and the 20 ms kept are run down by the cheap steps after it. 0 B after the first step, drops included.
        [Test]
        public void ASlowStretchCostsOneBoundedHiccup()
        {
            byte[] log = FlySlow(out StepWatch watch, out long allocated);
            FlightSummary s = FlightSummary.Read(new MemoryStream(log));
            Assert.That(s.Complete, s.CutReason);
            List<LoggedEvent> drops = s.Events.FindAll(e => e.Event.Kind == EventKind.Hitch && e.Event.Index == (byte)HitchCause.Drop);

            long droppedUs = 0, maxOwedUs = long.MinValue;
            int d = 0;
            for (long k = 0; k < SlowSteps; k++)
            {
                for (; d < drops.Count && drops[d].Step <= k + 1; d++) droppedUs += (long)drops[d].Event.B; // stamped with the step's state
                long hostUs = StartUs + k * FlightCore.StepUs + droppedUs;
                long owedUs = watch.StartUs[k] - hostUs;
                maxOwedUs = Math.Max(maxOwedUs, owedUs);
                Assert.That(owedUs, Is.InRange(0, FlightScheduler.MaxOwedUs), "step " + k + " started that late");
                Assert.That(watch.StickUs[k], Is.InRange(hostUs - SlowClock.ReportUs + 1, hostUs), "step " + k + " read the stick in force at its host time");
                Assert.That(watch.StartUs[k] - watch.StickUs[k], Is.LessThan(FlightScheduler.MaxOwedUs + SlowClock.ReportUs), "step " + k + ": its stick's age");
            }
            TestContext.WriteLine("500 steps of 3 ms: " + drops.Count + " drops, " + droppedUs + " us dropped, at most " + maxOwedUs + " us owed; "
                                  + s.End.Hitches + " hitches; " + s.End.Times);
            Assert.That(maxOwedUs, Is.EqualTo(FlightScheduler.MaxOwedUs), "the lag reaches the cap and stops there");
            Assert.That(drops.ConvertAll(e => (e.Event.A, e.Event.B)), Is.All.EqualTo((22000.0, 2000.0)), "each drop: how late, and how much it dropped");
            Assert.That((drops.Count, drops[0].Step, drops[drops.Count - 1].Step, droppedUs), Is.EqualTo((490, 1012L, 1501L, 980000L)));
            Assert.That(s.Steps, Is.EqualTo(SlowSteps), "every step run, each 1 ms");
            Assert.That((watch.Scheduler.StartUs, watch.Poses.Read().StartUs), Is.EqualTo((StartUs + droppedUs, StartUs + droppedUs)),
                "the time base, and the pose buffer's with it, moved by every drop");
            Assert.That(allocated, Is.Zero, "0 B a step after the first, drops included");
        }

        // "The replay ignores the drop": the slow flight's log, its 490 drops moving the sticks' host times, replays bit for bit.
        [Test]
        public void TheReplayIgnoresTheDrop()
        {
            byte[] log = FlySlow(out _, out _);
            FlightSummary s = FlightSummary.Read(new MemoryStream(log));
            Assert.That(s.Events.FindAll(e => e.Event.Kind == EventKind.Hitch && e.Event.Index == (byte)HitchCause.Drop).Count, Is.EqualTo(490));
            ReplayResult r = FlightReplay.Run(new MemoryStream(log), OpenWorld, BootstrapDrone.Spec().ContentHash);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Match), r.Message);
            Assert.That(r.Steps, Is.EqualTo(SlowSteps));
        }

        // The brief's "old logs still read": a format-69 log (the same records, its end block the six counts alone) reads whole
        // and replays bit for bit; its summary says it holds no step times. Another format is refused.
        [Test]
        public void AFormat69LogStillReads()
        {
            var log = new MemoryStream();
            LogHeader header = Record(log, 300);
            byte[] bytes = log.ToArray();
            long headerLength = header.Length;
            header.FormatVersion = LogFormat.Format69;
            header.EndFields = LogFormat.Format69EndFields;
            byte[] head = header.Encode();
            var old = new MemoryStream();
            old.Write(head, 0, head.Length);
            old.Write(bytes, (int)headerLength, (int)(bytes.Length - headerLength - LogFormat.BlockHeaderSize - LogFormat.EndSize));
            var end = new byte[LogFormat.BlockHeaderSize + LogFormat.Format69EndSize];
            LogFormat.EncodeBlockHeader(end, LogFormat.EndBlock, LogFormat.Format69EndSize);
            Array.Copy(bytes, bytes.Length - LogFormat.EndSize, end, LogFormat.BlockHeaderSize, LogFormat.Format69EndSize);
            old.Write(end, 0, end.Length);
            byte[] format69 = old.ToArray();
            Assert.That(BitConverter.ToUInt32(format69, 8), Is.EqualTo((uint)LogFormat.Format69), "the prologue's version");

            FlightSummary s = FlightSummary.Read(new MemoryStream(format69));
            Assert.That(s.HeaderProblem, Is.Null);
            Assert.That(s.Complete, s.CutReason);
            Assert.That((s.Steps, s.End.Steps), Is.EqualTo((300L, 300L)));
            Assert.That(s.End.Times, Is.EqualTo(default(StepTimeSummary)));
            var text = new StringWriter();
            s.Print(text);
            StringAssert.Contains("format 69, model " + LogFormat.ModelVersion, text.ToString());
            StringAssert.Contains("step times: not in a format-69 log", text.ToString());
            ReplayResult r = FlightReplay.Run(new MemoryStream(format69), OpenWorld, BootstrapDrone.Spec().ContentHash);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Match), r.Message);
            Assert.That(r.Steps, Is.EqualTo(300));

            format69[8] = 70;
            FlightSummary other = FlightSummary.Read(new MemoryStream(format69));
            Assert.That(other.Header, Is.Null);
            StringAssert.Contains("format is version 70; this build reads versions 72 and 69", other.HeaderProblem);
        }

        [Test]
        public void ATruncatedLogStillReads()
        {
            var full = new MemoryStream();
            Record(full, 3000);
            byte[] bytes = full.ToArray();
            FlightSummary whole = FlightSummary.Read(new MemoryStream(bytes));
            Assert.That(whole.Complete, Is.True);
            Assert.That(whole.Steps, Is.EqualTo(3000));
            Assert.That(whole.CutAt, Is.EqualTo(-1));

            // The record the cut logs end on, from the whole log.
            var reader = new FlightLogReader(new MemoryStream(bytes));
            var lastWhole = new byte[LogFormat.StepSize];
            while (reader.Next())
                if (reader.Kind == LogFormat.StepBlock && LogFormat.StepOf(reader.Payload) == 1233)
                    reader.Payload.CopyTo(lastWhole);

            long block = BlockOf(bytes, 1234);
            var cuts = new[]
            {
                (at: block + LogFormat.BlockHeaderSize + 100, reason: "cut inside a step block, 100 of its " + LogFormat.StepSize + " bytes"),
                (at: block + 2, reason: "cut inside a block's header"),
                (at: block, reason: "ends after a whole block, without its end block"),
            };
            foreach (var cut in cuts)
            {
                FlightSummary s = FlightSummary.Read(new MemoryStream(Head(bytes, cut.at)));
                Assert.That(s.Header, Is.Not.Null);
                Assert.That(s.Complete, Is.False, cut.reason);
                Assert.That(s.CutAt, Is.EqualTo(block), "where it was cut: the first byte of the incomplete record");
                StringAssert.Contains(cut.reason, s.CutReason);
                Assert.That(s.Steps, Is.EqualTo(1233), "up to the last whole record");
                Assert.That(s.Last.State.Step, Is.EqualTo(1233));
                var mine = new byte[LogFormat.StepSize];
                LogFormat.EncodeStep(mine, s.Last.State, s.Last.Stick, s.Last.Trace);
                Assert.That(mine, Is.EqualTo(lastWhole), "the last whole record reads bit for bit");
                Assert.That(s.Count(EventKind.Hitch), Is.EqualTo(2), "the stall's drop and late pass at 0.7 s are before the cut");

                var text = new StringWriter();
                s.Print(text);
                StringAssert.Contains("CUT at byte " + block, text.ToString());
                StringAssert.Contains("step 1233", text.ToString());

                ReplayResult r = FlightReplay.Run(new MemoryStream(Head(bytes, cut.at)), OpenWorld, BootstrapDrone.Spec().ContentHash);
                Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.MatchCut), r.Message);
                Assert.That(r.Steps, Is.EqualTo(1233));
            }

            // Cut inside the header: no flight to read, and the summary says where.
            FlightSummary none = FlightSummary.Read(new MemoryStream(Head(bytes, 100)));
            Assert.That(none.Header, Is.Null);
            StringAssert.Contains("cut inside its header, at byte 100", none.HeaderProblem);
        }

        [Test]
        public void AnotherWorldModelOrSettingsIsRefused()
        {
            ulong drone = BootstrapDrone.Spec().ContentHash;
            var log = new MemoryStream();
            Record(log, 200);
            byte[] bytes = log.ToArray();

            var raised = new FlightWorld(new FloorWorld(0.5, "belt_bare", "steel"));
            ReplayResult world = FlightReplay.Run(new MemoryStream(bytes), id => raised, drone);
            Assert.That(world.Outcome, Is.EqualTo(ReplayOutcome.Refused), world.Message);
            StringAssert.Contains("world differs", world.Message);
            StringAssert.Contains(FlightWorld.Floor().ContentHash.ToString("X16"), world.Message);
            StringAssert.Contains(raised.ContentHash.ToString("X16"), world.Message);

            ReplayResult unknown = FlightReplay.Run(new MemoryStream(bytes), id => null, drone);
            Assert.That(unknown.Outcome, Is.EqualTo(ReplayOutcome.Refused));
            StringAssert.Contains("cannot open the world 'floor'", unknown.Message);

            // The first header entry is the flight's: a u16 tag, a u32 length, then the model version.
            byte[] model = (byte[])bytes.Clone();
            Assert.That(BitConverter.ToUInt16(model, LogHeader.PrologueSize), Is.EqualTo(1));
            model[LogHeader.PrologueSize + 6] = 99;
            ReplayResult other = FlightReplay.Run(new MemoryStream(model), OpenWorld, drone);
            Assert.That(other.Outcome, Is.EqualTo(ReplayOutcome.Refused), other.Message);
            StringAssert.Contains("model version 99", other.Message);

            var settings = new MemoryStream();
            Record(settings, 10, h =>
            {
                var list = new List<SettingValue>(h.Settings) { new SettingValue("no_such_setting", SettingValue.Double, 0) };
                h.Settings = list.ToArray();
            });
            settings.Position = 0;
            ReplayResult fc = FlightReplay.Run(settings, OpenWorld, drone);
            Assert.That(fc.Outcome, Is.EqualTo(ReplayOutcome.Refused), fc.Message);
            StringAssert.Contains("no_such_setting", fc.Message);

            ReplayResult garbage = FlightReplay.Run(new MemoryStream(Encoding.ASCII.GetBytes("not a flight log at all")), OpenWorld, drone);
            Assert.That(garbage.Outcome, Is.EqualTo(ReplayOutcome.Unreadable));
        }

        [Test]
        public void ADivergenceNamesTheStepAndField()
        {
            var log = new MemoryStream();
            Record(log, 1000);
            byte[] bytes = log.ToArray();
            int field = Array.FindIndex(LogFormat.StepFields, f => f.Name == "attitude.y");
            long at = BlockOf(bytes, 777) + LogFormat.BlockHeaderSize + LogFormat.StepFields[field].Offset;
            bytes[at] ^= 1; // the last bit of the mantissa

            ReplayResult r = FlightReplay.Run(new MemoryStream(bytes), OpenWorld, BootstrapDrone.Spec().ContentHash);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Diverged), r.Message);
            Assert.That(r.Step, Is.EqualTo(777));
            Assert.That(r.Field, Is.EqualTo("attitude.y"));
            Assert.That(r.Steps, Is.EqualTo(776), "matched up to the step before");
            StringAssert.Contains("diverged at step 777", r.Message);
        }

        // A stream whose writes block while its gate is shut: a stalled disk.
        sealed class GateStream : MemoryStream
        {
            readonly ManualResetEventSlim _open = new ManualResetEventSlim(true);

            public void Shut() => _open.Reset();

            public void Open() => _open.Set();

            public override void Write(byte[] buffer, int offset, int count)
            {
                _open.Wait();
                base.Write(buffer, offset, count);
            }
        }

        [Test]
        public void AFullRingWaitsAndLogsAHitch()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            FlightSetup setup = Setup(spec);
            var core = new FlightCore(setup);
            var disk = new GateStream();
            var recorder = new FlightRecorder(disk, Header(setup, core, spec), FlightRecorder.MinCapacity, ownsStream: false);
            disk.Shut(); // a 16 KiB ring holds 17 steps

            var opener = new Thread(() =>
            {
                Thread.Sleep(100);
                disk.Open();
            });
            opener.Start();
            for (int k = 0; k < 2000; k++)
            {
                StickSample stick = Report(k, StartUs + k * FlightCore.StepUs);
                core.Advance(stick);
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
            }
            opener.Join();
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            Assert.That(recorder.Counts.LogWaits, Is.GreaterThanOrEqualTo(1), "the flight thread waited for the stalled disk");

            disk.Position = 0;
            FlightSummary s = FlightSummary.Read(disk);
            Assert.That(s.Steps, Is.EqualTo(2000), "nothing dropped");
            Assert.That(s.Gaps, Is.EqualTo(0));
            Assert.That(s.Count(EventKind.Hitch), Is.EqualTo(recorder.Counts.LogWaits));
            LoggedEvent hitch = s.Events.Find(e => e.Event.Kind == EventKind.Hitch);
            Assert.That(hitch.Event.Index, Is.EqualTo((byte)HitchCause.LogFull));
            Assert.That(hitch.Event.A, Is.GreaterThanOrEqualTo(1), "the 1 ms waits");

            disk.Position = 0;
            ReplayResult r = FlightReplay.Run(disk, OpenWorld, spec.ContentHash);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Match), r.Message);
        }

        [Test]
        public void TheHeaderKeepsTheSetupAndSimDataOnly()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            FlightSetup setup = Setup(spec);
            FcSettings fc = FcSettings.Default;
            fc.p_roll = 66.5;
            fc.airmode = false;
            fc.iterm_relax = ItermRelax.RPY_INC;
            setup.Settings = fc;
            var core = new FlightCore(setup);
            SeedStream stream = core.Random.Open("test.parameter");
            double drawn = core.Random.Draw(ref stream, "test.parameter", 0.9, 1.1);
            LogHeader h = Header(setup, core, spec);
            h.Input = InputSource.Radio;
            h.InputDevice = "Test Radio Joystick";
            h.InputIntervals = new ReportIntervals();
            foreach (long us in new long[] { 1980, 2010, 2040, 950, 25000 }) h.InputIntervals.Add(us);

            byte[] bytes = h.Encode();
            Assert.That(h.Length, Is.EqualTo(bytes.Length));
            LogHeader back = LogHeader.Read(new MemoryStream(bytes));
            Assert.That(back.Unknown, Is.Empty);
            Assert.That(back.LayoutsDiffer(out _), Is.False);
            Assert.That(back.Seed, Is.EqualTo(setup.Seed));
            Assert.That(back.ModelVersion, Is.EqualTo(LogFormat.ModelVersion));
            Assert.That((back.Build, back.Runtime, back.Isa), Is.EqualTo(("simtests", LogHeader.CurrentRuntime(), "test")));
            Assert.That((back.WorldId, back.WorldHash, back.QueryVersion), Is.EqualTo(("floor", setup.World.ContentHash, setup.World.QueryVersion)));
            Assert.That(back.SurfaceName(1), Is.EqualTo("belt_bare"));
            Assert.That(back.MaterialName(1), Is.EqualTo("steel"));
            Assert.That((back.DroneHash, back.DroneName), Is.EqualTo((spec.ContentHash, spec.Name)));
            Assert.That(back.Loadout, Is.EqualTo(spec.Nominal));
            Assert.That((back.StartPosition, back.StartAttitude, back.StartVelocity, back.StartBodyRate),
                Is.EqualTo((setup.StartPosition, setup.StartAttitude, setup.StartVelocity, setup.StartBodyRate)));
            Assert.That((back.Input, back.InputDevice), Is.EqualTo((InputSource.Radio, "Test Radio Joystick")));
            Assert.That(back.InputIntervals.Counts, Is.EqualTo(h.InputIntervals.Counts));
            Assert.That((back.InputIntervals.BinUs, back.InputIntervals.Count, back.InputIntervals.SumUs, back.InputIntervals.MinUs, back.InputIntervals.MaxUs),
                Is.EqualTo((100, 5L, 31980L, 950L, 25000L)));
            Assert.That(back.InputIntervals.PercentileUs(50), Is.EqualTo(2100), "the bin of 2010 us");
            Assert.That(back.Streams, Is.EqualTo(new[] { "wind.direction", "wind.strength", "wind.meander", "wind.shift", "wind.turbulence", "test.parameter" }));
            Assert.That(back.Draws.Length, Is.EqualTo(3), "the wind's two and the test's");
            for (int i = 0; i < 2; i++) Assert.That(back.Draws[i], Is.EqualTo(core.Random.Draws[i]), "wind draw " + i);
            ParameterDraw last = back.Draws[2];
            Assert.That((last.Parameter, last.Lo, last.Hi, last.Value, last.Index), Is.EqualTo(("test.parameter", 0.9, 1.1, drawn, 0UL)));

            // The air: the drag spec field for field, and the wind's model, level and preset.
            foreach (string f in LogHeader.AeroFieldsLogged)
            {
                FieldInfo info = typeof(AeroSpec).GetField(f);
                Assert.That(info.GetValue(back.Aero), Is.EqualTo(info.GetValue(setup.Aero)), "aero." + f);
            }
            Assert.That((back.WindModel, back.Wind), Is.EqualTo(("field", setup.Wind)));
            Assert.That(back.WindParameters, Is.EqualTo(LogHeader.PresetOf(WindLevel.Windy)));
            Assert.That(back.WindParameters.Length, Is.EqualTo(37), "every field of the preset but its level");

            var problems = new List<string>();
            back.ReadSettings(out FcSettings read, problems);
            Assert.That(problems, Is.Empty);
            Assert.That(LogHeader.SettingsOf(read), Is.EqualTo(LogHeader.SettingsOf(fc)), "every setting, bit for bit");

            Assert.That(back.Parts.Length, Is.EqualTo(setup.Mass.PartCount));
            for (int i = 0; i < back.Parts.Length; i++)
                Assert.That(back.Parts[i], Is.EqualTo(setup.Mass.Part(i)), "part " + i);
            Assert.That(back.Body, Is.EqualTo(core.Body));
            Assert.That(new MassModel(back.Parts).Properties, Is.EqualTo(core.Body), "the logged parts rebuild the body");

            // The propulsion, every field and curve point, and the packs' start charge.
            Assert.That(back.StartPackCharge, Is.EqualTo(setup.StartPackCharge));
            PropulsionSpec a = setup.Propulsion, b = back.Propulsion;
            Assert.That(b, Is.Not.Null);
            Assert.That(b.Motors, Is.EqualTo(a.Motors));
            foreach (string f in LogHeader.PropsFieldsLogged)
            {
                FieldInfo info = typeof(DroneProps).GetField(f);
                Assert.That(info.GetValue(b.Props), Is.EqualTo(info.GetValue(a.Props)), "props." + f);
            }
            for (int k = 0; k < 2; k++)
                foreach (string f in LogHeader.PackFieldsLogged)
                {
                    FieldInfo info = typeof(DronePack).GetField(f);
                    Assert.That(info.GetValue(b.Packs[k]), Is.EqualTo(info.GetValue(a.Packs[k])), "packs[" + k + "]." + f);
                }

            // LOG-05: no user, machine or path in the log.
            string text = Encoding.UTF8.GetString(bytes);
            foreach (string personal in new[]
                     {
                         Environment.UserName, Environment.MachineName, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                         Path.GetTempPath(), Directory.GetCurrentDirectory(),
                     })
                if (!string.IsNullOrEmpty(personal) && personal.Length > 2)
                    StringAssert.DoesNotContain(personal, text);
        }

        // p7f-damage 1.1b: a flight from the repository's drone.json enters its blade and its motors' internals, bit for bit,
        // with the file's hash; a header whose tables name another drone.json is refused, and a bench body enters none.
        [Test]
        public void TheHeaderKeepsTheBladeAndTheMotorsInternals()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = Setup(spec);
            var core = new FlightCore(setup);
            byte[] bytes = Header(setup, core, spec).Encode();
            LogHeader back = LogHeader.Read(new MemoryStream(bytes));
            Assert.That(back.Unknown, Is.Empty);
            Assert.That(back.Blade, Is.Not.Null);
            foreach (string f in LogHeader.BladeFieldsLogged)
            {
                FieldInfo info = typeof(PropBlade).GetField(f);
                Assert.That(info.GetValue(back.Blade), Is.EqualTo(info.GetValue(spec.Blade)), "blade." + f);
            }
            Assert.That(back.Internals, Is.EqualTo(spec.Internals));

            // The entry: tag 26 (25 on p7f-damage; since format 30 the line's tether entry keeps 25), its payload led by drone.json's hash.
            int at = LogHeader.PrologueSize, payload = -1;
            while (at < bytes.Length)
            {
                int tag = bytes[at] | bytes[at + 1] << 8, size = BitConverter.ToInt32(bytes, at + 2);
                if (tag == 26) payload = at + 6;
                at += 6 + size;
            }
            Assert.That(BitConverter.ToUInt64(bytes, payload), Is.EqualTo(spec.ContentHash));
            bytes[payload] ^= 1;
            var e = Assert.Throws<FlightLogException>(() => LogHeader.Read(new MemoryStream(bytes)));
            StringAssert.Contains("blade and motor tables are of drone.json", e.Message);

            LogHeader bench = LogHeader.Read(new MemoryStream(LogHeader.For(setup, core).Encode()));
            Assert.That(bench.Blade, Is.Null);
            Assert.That(bench.Internals, Is.Null);
        }

        // p7f-damage 1.2 (format 23): the propulsion entry carries the props' blades as elements (the blade's table and the
        // sections' constants) and the asymmetry entry the blades' pitch bound and count, so a replay rebuilds the same rotors and
        // draws the same pitch offsets; a propulsion without them reads back without them.
        [Test]
        public void TheHeaderKeepsTheBladesAsElementsAndThePitchBound()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = GoldenFlights.PoweredSetup(spec);
            var core = new FlightCore(setup);
            LogHeader back = LogHeader.Read(new MemoryStream(Header(setup, core, spec).Encode()));
            Assert.That(back.Unknown, Is.Empty);
            DamagedRotorSpec a = setup.Propulsion.Rotor, b = back.Propulsion.Rotor;
            Assert.That(b, Is.Not.Null);
            foreach (string f in LogHeader.RotorFieldsLogged)
            {
                if (f == "Blade") continue;
                FieldInfo info = typeof(DamagedRotorSpec).GetField(f);
                Assert.That(info.GetValue(b), Is.EqualTo(info.GetValue(a)), "rotor." + f);
            }
            foreach (string f in LogHeader.BladeFieldsLogged)
            {
                FieldInfo info = typeof(PropBlade).GetField(f);
                Assert.That(info.GetValue(b.Blade), Is.EqualTo(info.GetValue(a.Blade)), "rotor.blade." + f);
            }
            Assert.That((back.Asymmetry.BladePitchBound, back.Asymmetry.Blades), Is.EqualTo((setup.Asymmetry.BladePitchBound, setup.Asymmetry.Blades)));
            var problems = new List<string>();
            var replayed = new FlightCore(back.ToSetup(setup.World, problems));
            Assert.That(problems, Is.Empty);
            Assert.That(replayed.Draws.BladePitch, Is.EqualTo(core.Draws.BladePitch), "the replay draws the same pitch offsets");
            for (int i = 0; i < 4; i++)
                Assert.That(replayed.Propulsion.Rotor(i).ThrustRatio(0), Is.EqualTo(core.Propulsion.Rotor(i).ThrustRatio(0)), "rotor " + i + "'s tables");

            LogHeader bare = LogHeader.Read(new MemoryStream(LogHeader.For(Setup(BootstrapDrone.Spec()), new FlightCore(Setup(BootstrapDrone.Spec()))).Encode()));
            Assert.That(bare.Propulsion.Rotor, Is.Null, "a bootstrap drone has no blade");
        }

        [Test]
        public void TheRecordsCoverTheStateAndTheSetup()
        {
            Assert.That(InstanceFields(typeof(FlightState)), Is.EquivalentTo(LogFormat.StateFieldsLogged),
                "a new FlightState field is logged: extend the step record (a new LogFormat.Version)");
            Assert.That(InstanceFields(typeof(StickSample)), Is.EquivalentTo(LogFormat.StickFieldsLogged));
            Assert.That(InstanceFields(typeof(StepTrace)), Is.EquivalentTo(LogFormat.TraceFieldsLogged));
            Assert.That(InstanceFields(typeof(FlightSetup)), Is.EquivalentTo(LogHeader.SetupFieldsLogged),
                "a new FlightSetup field goes into the header, so a replay rebuilds it");
            Assert.That(InstanceFields(typeof(DronePart)), Is.EquivalentTo(LogHeader.PartFieldsLogged));
            Assert.That(InstanceFields(typeof(FcState)), Is.EquivalentTo(LogFormat.FcFieldsLogged),
                "a new FcState field is logged: extend the step record (a new LogFormat.Version)");
            Assert.That(InstanceFields(typeof(Axes3)), Is.EquivalentTo(new[] { "Roll", "Pitch", "Yaw" }));
            Assert.That(InstanceFields(typeof(PropulsionSpec)), Is.EquivalentTo(LogHeader.PropulsionFieldsLogged));
            Assert.That(InstanceFields(typeof(DroneMotor)), Is.EquivalentTo(LogHeader.MotorFieldsLogged));
            Assert.That(InstanceFields(typeof(DroneProps)), Is.EquivalentTo(LogHeader.PropsFieldsLogged));
            Assert.That(InstanceFields(typeof(DronePack)), Is.EquivalentTo(LogHeader.PackFieldsLogged));
            Assert.That(InstanceFields(typeof(AeroSpec)), Is.EquivalentTo(LogHeader.AeroFieldsLogged));
            Assert.That(InstanceFields(typeof(WindSetup)), Is.EquivalentTo(LogHeader.WindFieldsLogged));
            Assert.That(InstanceFields(typeof(MotorAsymmetry)), Is.EquivalentTo(LogHeader.AsymmetryFieldsLogged));
            Assert.That(InstanceFields(typeof(BuildTolerance)), Is.EquivalentTo(LogHeader.PlacementFieldsLogged));
            Assert.That(InstanceFields(typeof(VortexRingSpec)), Is.EquivalentTo(LogHeader.VortexRingFieldsLogged));
            Assert.That(InstanceFields(typeof(GroundEffectSpec)), Is.EquivalentTo(LogHeader.GroundEffectFieldsLogged));
            Assert.That(InstanceFields(typeof(RotorFlappingSpec)), Is.EquivalentTo(LogHeader.FlappingFieldsLogged));
            Assert.That(InstanceFields(typeof(RotorWakeSpec)), Is.EquivalentTo(LogHeader.RotorWakeFieldsLogged));
            Assert.That(InstanceFields(typeof(RotorInterferenceSpec)), Is.EquivalentTo(LogHeader.InterferenceFieldsLogged));
            Assert.That(InstanceFields(typeof(AirframeModesSpec)), Is.EquivalentTo(LogHeader.ModesFieldsLogged));
            Assert.That(InstanceFields(typeof(Fpv450.Sim.Tether.TetherTable)), Is.EquivalentTo(LogHeader.TetherFieldsLogged));
            Assert.That(InstanceFields(typeof(Fpv450.Sim.Tether.TetherRow)), Is.EquivalentTo(LogHeader.TetherRowFieldsLogged));
            Assert.That(InstanceFields(typeof(Fpv450.Sim.Tether.TetherTrace)), Is.EquivalentTo(LogFormat.TetherTraceFieldsLogged));
            Assert.That(InstanceFields(typeof(PropBlade)), Is.EquivalentTo(LogHeader.BladeFieldsLogged));
            Assert.That(InstanceFields(typeof(MotorInternals)), Is.EquivalentTo(LogHeader.InternalsFieldsLogged));
            Assert.That(InstanceFields(typeof(DamagedRotorSpec)), Is.EquivalentTo(LogHeader.RotorFieldsLogged));

            foreach ((LogField[] fields, int size) in new[] { (LogFormat.StepFields, LogFormat.StepSize), (LogFormat.EventFields, LogFormat.EventSize),
                         (LogFormat.EndFields, LogFormat.EndSize) })
            {
                int at = 0;
                foreach (LogField f in fields)
                {
                    Assert.That(f.Offset, Is.EqualTo(at), f.Name + " follows the field before it");
                    at += f.Size;
                }
                Assert.That(at, Is.EqualTo(size));
            }

            // Every field of a step and an event, each a distinct value, through the encoder and back.
            var s = new FlightState
            {
                Step = 123456789, Position = new Double3(1.5, -2.25, 3.125), Velocity = new Double3(4.5, 5.5, -6.5),
                Attitude = new Quat(0.1, -0.2, 0.3, 0.9), BodyRate = new Double3(7, 8, -9), Armed = true,
                RotorSpeed = new Corners { RearRight = 10, FrontRight = 11, RearLeft = 12, FrontLeft = 13 },
                EscCommand = new Corners { RearRight = 0.14, FrontRight = 0.15, RearLeft = 0.16, FrontLeft = 0.17 },
                PackCharge0 = 0.18, PackCharge1 = 0.19, EscLimited = 0x9,
            };
            // Every FcState double a distinct value through reflection, its flags every bit.
            object fc = default(FcState);
            double v = 100;
            foreach (FieldInfo f in typeof(FcState).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(double)) f.SetValue(fc, v++);
                else if (f.FieldType == typeof(Axes3)) f.SetValue(fc, new Axes3 { Roll = v++, Pitch = v++, Yaw = v++ });
                else if (f.FieldType == typeof(FcFlags)) f.SetValue(fc, (FcFlags)0x3FF);
                else if (f.FieldType == typeof(byte)) f.SetValue(fc, (byte)v++);
                else Assert.Fail("FcState." + f.Name + " has a type the log does not encode");
            }
            s.Fc = (FcState)fc;
            Assert.That(v, Is.EqualTo(100 + 91 + 3), "the record's 91 controller doubles and 3 bytes");
            var k = new StickSample { HostUs = -987654321, Roll = -0.2, Pitch = 0.21, Yaw = -0.22, Throttle = 0.23, Arm = false, Failsafe = true };
            var t = new StepTrace
            {
                RotorCommand = new Corners { RearRight = 0.24, FrontRight = 0.25, RearLeft = 0.26, FrontLeft = 0.27 },
                PackVolts0 = 24.8, PackVolts1 = 24.9, PackAmps0 = 30.1, PackAmps1 = 30.2,
                NetForce = new Double3(31, -32, 33), NetMoment = new Double3(-0.34, 0.35, -0.36),
                AeroForce = new Double3(-1.5, 2.5, 3.5), AeroMoment = new Double3(0.04, -0.05, 0.06),
                Wind = new Double3(0.5, -0.25, 0.75),
                RotorWind = new Corners3
                {
                    RearRight = new Double3(1.25, -0.5, 2), FrontRight = new Double3(-1.75, 0.125, 2.5), RearLeft = new Double3(3, 0.0625, -3.5),
                    FrontLeft = new Double3(-4, 0.375, 4.5),
                },
            };
            var bytes = new byte[LogFormat.StepSize];
            LogFormat.EncodeStep(bytes, s, k, t);
            LogFormat.DecodeStep(bytes, out FlightState s2, out StickSample k2, out StepTrace t2);
            Assert.That(s2, Is.EqualTo(s));
            Assert.That(k2, Is.EqualTo(k));
            Assert.That(t2, Is.EqualTo(t));
            LogFormat.DecodeStick(bytes, out StickSample k3);
            Assert.That(k3, Is.EqualTo(k));
            // The crash (bit 2, 1.6) and the stick's failsafe (bit 3, p6-input-app) are separate bits.
            s.Crashed = true;
            k.Failsafe = false;
            LogFormat.EncodeStep(bytes, s, k, t);
            LogFormat.DecodeStep(bytes, out s2, out k2, out _);
            Assert.That((s2, k2), Is.EqualTo((s, k)));
            Assert.That(bytes[Array.Find(LogFormat.StepFields, f => f.Name == "flags").Offset], Is.EqualTo(0b0101), "armed and crashed");

            var e = new FlightEvent
            {
                Kind = EventKind.Crash, Part = 7, Index = 2, Object = 1234, Shape = 5, Material = 6, Surface = 3,
                Point = new Double3(1, 2, 3), A = 4.5, B = 6.5, C = 490.3,
            };
            var eb = new byte[LogFormat.EventSize];
            LogFormat.EncodeEvent(eb, 42, e);
            LogFormat.DecodeEvent(eb, out long step, out FlightEvent e2);
            Assert.That((step, e2), Is.EqualTo((42L, e)));
        }

        // A recorder handed a step's events directly, until the producers exist (1.4, 1.6).
        [Test]
        public void EventsAreLoggedAndReplayed()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            FlightSetup setup = Setup(spec);
            var core = new FlightCore(setup);
            var log = new MemoryStream();
            var recorder = new FlightRecorder(log, Header(setup, core, spec), ownsStream: false);
            var events = new StepEvents();
            FlightEvent contact = FlightEvent.Of(EventKind.ContactMade);
            contact.Part = 3;
            contact.Object = WorldIds.Ground;
            contact.Surface = 1;
            contact.Point = new Double3(0.2, 0, 0.3);
            contact.A = 1.25;
            for (int n = 1; n <= 10; n++)
            {
                core.Advance(StickSample.Neutral);
                events.Clear();
                if (n == 5) events.Add(contact);
                if (n == 7)
                    for (int i = 0; i < 65; i++)
                        events.Add(FlightEvent.Of(EventKind.MixerSaturation));
                recorder.RecordStep(core.State, StickSample.Neutral, core.Trace, events);
            }
            recorder.Close();

            log.Position = 0;
            FlightSummary s = FlightSummary.Read(log);
            Assert.That(s.Events.Count, Is.EqualTo(1 + 64 + 1));
            Assert.That((s.Events[0].Step, s.Events[0].Event), Is.EqualTo((5L, contact)));
            Assert.That(s.Count(EventKind.MixerSaturation), Is.EqualTo(64));
            LoggedEvent lost = s.Events[s.Events.Count - 1];
            Assert.That((lost.Step, lost.Event.Kind, lost.Event.A), Is.EqualTo((7L, EventKind.EventsLost, 1.0)));
            Assert.That(s.End.EventsLost, Is.EqualTo(1));
            StringAssert.Contains("touches the ground (surface belt_bare)", s.Describe(contact));

            // The free body makes no events, so the replay finds the logged contact it did not make.
            log.Position = 0;
            ReplayResult r = FlightReplay.Run(log, OpenWorld, spec.ContentHash);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Diverged), r.Message);
            Assert.That(r.Step, Is.EqualTo(5));
            StringAssert.Contains("ContactMade event at step 5 that the replay did not make", r.Message);
        }

        // QA's p7b-airframe finding 5: the summary names every airframe effect, on or off, once each. Each flight records the
        // powered golden's setup for 20 steps with one effect off (and one with all of them off), and its summary says so.
        [TestCase("motor asymmetry", "motor asymmetry: off (identical rotors)")]
        [TestCase("vortex rings", "vortex rings: off")]
        [TestCase("ground effect", "ground effect: off")]
        [TestCase("rotor wake", "rotor wake: off (KNOWN-WRONG for a pilot's flight: a perfectly steady rotor)")]
        [TestCase("flapping", "flapping: off")]
        [TestCase("rotor interference", "rotor interference: off")]
        [TestCase("modes", "modes: off (a rigid airframe)")]
        [TestCase(null, null)]
        public void TheSummaryNamesEveryAirframeEffectOnOrOff(string offName, string offLine)
        {
            string[] names = { "motor asymmetry", "vortex rings", "ground effect", "rotor wake", "flapping", "rotor interference", "modes" };
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (bool allOff in offName == null ? new[] { false, true } : new[] { false })
            {
                FlightSetup setup = GoldenFlights.PoweredSetup(spec);
                bool Off(string n) => allOff || n == offName;
                if (Off("motor asymmetry")) setup.Asymmetry = null;
                if (Off("vortex rings")) setup.VortexRing = null;
                if (Off("ground effect")) setup.GroundEffect = null;
                if (Off("rotor wake")) setup.RotorWake = null;
                if (Off("flapping")) setup.Flapping = null;
                if (Off("rotor interference")) setup.Interference = null;
                if (Off("modes")) setup.Modes = null;
                var core = new FlightCore(setup);
                LogHeader h = LogHeader.For(setup, core);
                h.Build = "simtests";
                h.Isa = "test";
                h.WorldId = "floor";
                h.SetDrone(spec, spec.Nominal);
                var log = new MemoryStream();
                var recorder = new FlightRecorder(log, h, FlightRecorder.DefaultCapacity, ownsStream: false);
                for (int k = 0; k < 20; k++)
                {
                    StickSample stick = GoldenFlights.PoweredStick(k);
                    core.Advance(stick);
                    recorder.RecordStep(core.State, stick, core.Trace, core.Events);
                }
                recorder.Close();
                var text = new StringWriter();
                FlightSummary.Read(new MemoryStream(log.ToArray())).Print(text);
                string summary = text.ToString();
                foreach (string n in names)
                {
                    int lines = 0;
                    foreach (string line in summary.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None))
                        if (line.TrimStart().StartsWith(n + ":", StringComparison.Ordinal)) lines++;
                    Assert.That(lines, Is.EqualTo(1), $"one line for {n}: {summary}");
                    string off = n == "motor asymmetry" ? "motor asymmetry: off (identical rotors)" : n == "modes" ? "modes: off (a rigid airframe)" : n + ": off";
                    if (Off(n)) StringAssert.Contains(off, summary);
                    else StringAssert.DoesNotContain(off, summary);
                }
                if (offLine != null) StringAssert.Contains(offLine, summary);
            }
        }

        [Test]
        public void TheDroneHashSeesEditsNotLineEndings()
        {
            string text = BootstrapDrone.Text().Replace("\r\n", "\n");
            Assert.That(DroneSpec.HashText(text.Replace("\n", "\r\n")), Is.EqualTo(DroneSpec.HashText(text)));
            Assert.That(DroneSpec.HashText(text + " "), Is.Not.EqualTo(DroneSpec.HashText(text)));
            Assert.That(BootstrapDrone.Spec().ContentHash, Is.EqualTo(DroneSpec.HashText(text)));
        }

        [Test]
        public void LogsLiveUnderTheUsersProfile()
        {
            string profileData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            StringAssert.StartsWith(profileData, LogStore.Folder);
            StringAssert.EndsWith(Path.Combine("450fpv", "flights"), LogStore.Folder);

            string folder = Path.Combine(Path.GetTempPath(), "fpv450-logstore-" + Guid.NewGuid().ToString("N"));
            try
            {
                string first = LogStore.NewPath(0x450F, folder);
                Assert.That(Path.GetFileName(first), Is.EqualTo("flight-000000000000450f-1.flightlog"));
                File.WriteAllBytes(first, new byte[0]);
                Assert.That(Path.GetFileName(LogStore.NewPath(0x450F, folder)), Is.EqualTo("flight-000000000000450f-2.flightlog"));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        static string[] InstanceFields(Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var names = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++) names[i] = fields[i].Name;
            return names;
        }
    }
}
