using System.Collections.Generic;
using System.Threading;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // flight-dynamics: "Render rate does not touch the flight" (PHB-01). The same seed and the same timestamped stick
    // samples, flown under a stub renderer at 30, 60 and 240 fps, with a 250 ms renderer stall, and with a 250 ms stall
    // of the whole process, give the same state and the same stick in force at every step; only the process stall logs
    // a hitch. Until task 1.2's log exists, the record is StateDump's per-step hash of the state and the stick.
    public sealed class RenderRateTests
    {
        const long StartUs = 5000000;
        const long DurationUs = 2000000;
        const long TickUs = 100;               // the harness's time resolution
        const long StallAtUs = StartUs + 700000;
        const long StallUs = 250000;

        enum Stall { None, Renderer, Process }

        sealed class VirtualClock : IHostClock
        {
            public long Now;
            public long NowUs() => Now;
        }

        static FlightSetup FreeBody() => new FlightSetup
        {
            Seed = 0x5EED,
            World = FlightWorld.Floor(),
            Mass = new MassModel(new[] { new DronePart { Name = "cube", Kind = PartKind.Box, Mass = 4.8, Size = new Double3(0.25, 0.25, 0.25) } }),
            StartPosition = new Double3(0, 10, 0),
            StartVelocity = new Double3(1, 4, -2),
            StartBodyRate = new Double3(1, 2, 0.5),
        };

        // A ~250 Hz report stream with jitter (3.7–4.3 ms apart), every tenth report stamped exactly on a step's host
        // time to test "at or before", armed from the 25th.
        static StickSample[] Sticks(long startUs, long durationUs)
        {
            var list = new List<StickSample>();
            long t = startUs - 2000;
            for (int i = 0; t < startUs + durationUs; i++)
            {
                if (i % 10 == 0 && t > startUs)
                    t = startUs + (t - startUs) / FlightCore.StepUs * FlightCore.StepUs;
                list.Add(new StickSample
                {
                    HostUs = t,
                    Roll = (i * 37 % 201 - 100) / 100.0,
                    Pitch = (i * 53 % 201 - 100) / 100.0,
                    Yaw = (i * 71 % 201 - 100) / 100.0,
                    Throttle = i * 29 % 101 / 100.0,
                    Arm = i >= 25,
                });
                t += 3700 + i * 131 % 600;
            }
            return list.ToArray();
        }

        static StateDump Fly(int fps, Stall stall, out int frames)
        {
            var clock = new VirtualClock { Now = StartUs };
            var core = new FlightCore(FreeBody());
            var inbox = new StickInbox();
            var poses = new PoseBuffer();
            var dump = new StateDump(2100);
            var scheduler = new FlightScheduler(core, inbox, poses, clock, dump);
            StickSample[] sticks = Sticks(StartUs, DurationUs);
            scheduler.Begin(StartUs);

            int next = 0;
            frames = 0;
            for (long t = StartUs; t <= StartUs + DurationUs; t += TickUs)
            {
                clock.Now = t;
                bool stalled = t >= StallAtUs && t < StallAtUs + StallUs;
                if (!(stall == Stall.Process && stalled))
                {
                    while (next < sticks.Length && sticks[next].HostUs <= t) inbox.TryWrite(sticks[next++]);
                    scheduler.RunDue(t);
                }
                if (!(stall != Stall.None && stalled) && t >= StartUs + frames * 1000000L / fps)
                {
                    ref readonly PosePair pair = ref poses.Read();
                    long from = pair.StartUs + pair.Previous.Step * FlightCore.StepUs;
                    long to = pair.StartUs + pair.Current.Step * FlightCore.StepUs;
                    Assert.That(t >= from && t < to, "frame " + frames + " at " + t + " is not between its two poses");
                    pair.Interpolate(t, out PoseFrame pose);
                    Assert.That(pose.Step, Is.EqualTo(pair.Current.Step));
                    frames++;
                }
            }
            return dump;
        }

        [Test]
        public void RenderRateDoesNotTouchTheFlight()
        {
            StateDump reference = Fly(60, Stall.None, out int frames60);
            Assert.That(reference.Count, Is.EqualTo((int)(DurationUs / FlightCore.StepUs) + 1));
            Assert.That(frames60, Is.EqualTo(121));
            Assert.That(reference.Hitches, Is.Empty);

            var runs = new[]
            {
                ("30 fps", Fly(30, Stall.None, out _)),
                ("240 fps", Fly(240, Stall.None, out _)),
                ("60 fps, 250 ms renderer stall", Fly(60, Stall.Renderer, out _)),
                ("60 fps, 250 ms process stall", Fly(60, Stall.Process, out _)),
            };
            foreach (var (name, dump) in runs)
            {
                Assert.That(StateDump.FirstDifference(reference, dump), Is.EqualTo(-1), name + ": first differing step");
                if (name.Contains("process"))
                {
                    Assert.That(dump.Hitches.Count, Is.EqualTo(1), name);
                    Assert.That(dump.Hitches[0].Steps, Is.GreaterThanOrEqualTo(StallUs / FlightCore.StepUs), name);
                    Assert.That(dump.Hitches[0].LateUs, Is.GreaterThanOrEqualTo(StallUs - FlightCore.StepUs), name);
                }
                else
                {
                    Assert.That(dump.Hitches, Is.Empty, name);
                }
            }
        }

        // The same flight on the real flight thread and the real clock, read by a renderer on this thread, matches a
        // plain loop applying the consumption rule independently, step for step.
        [Test]
        public void TheFlightThreadMatchesAPlainLoop()
        {
            const long runUs = 300000;
            var clock = HostClock.Instance;
            long start = clock.NowUs() + 20000;
            StickSample[] sticks = Sticks(start, runUs);
            var inbox = new StickInbox();
            foreach (StickSample s in sticks) Assert.That(inbox.TryWrite(s), Is.True);
            var poses = new PoseBuffer();
            var dump = new StateDump(4000);
            var scheduler = new FlightScheduler(new FlightCore(FreeBody()), inbox, poses, clock, dump);

            scheduler.Start(start);
            try
            {
                while (clock.NowUs() < start + runUs)
                {
                    ref readonly PosePair pair = ref poses.Read();
                    long gap = pair.Current.Step - pair.Previous.Step;
                    Assert.That(gap == 1 || (gap == 0 && pair.Current.Step == 0), "a torn or stale pair: " + pair.Previous.Step + ", " + pair.Current.Step);
                    pair.Interpolate(clock.NowUs(), out PoseFrame _);
                    Thread.Sleep(4);
                }
            }
            finally
            {
                scheduler.Stop();
            }
            Assert.That(scheduler.Fault, Is.Null);
            int n = System.Math.Min(dump.Count, 4000);
            Assert.That(n, Is.GreaterThanOrEqualTo((int)(runUs / FlightCore.StepUs)));

            var core = new FlightCore(FreeBody());
            StickSample inForce = StickSample.Neutral;
            int next = 0;
            for (int k = 0; k < n; k++)
            {
                long stepUs = start + k * FlightCore.StepUs;
                while (next < sticks.Length && sticks[next].HostUs <= stepUs) inForce = sticks[next++];
                core.Advance(inForce);
                Assert.That(StateDump.Hash(core.State, inForce), Is.EqualTo(dump[k]), "step " + (k + 1));
            }
        }
    }
}
