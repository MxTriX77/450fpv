using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;

namespace Fpv450.Replay
{
    // The scripted flight that exercises the log end to end (physics-log, "Replay matches the flight"): drone.json at
    // its nominal loadout with its Betaflight 4.5 controller and propulsion (1.4), its drag in a stand-in wind (1.5;
    // Windy unless asked otherwise), started 300 m up (the contacts arrive with 1.6), with a scripted pilot on the sticks. It is flown either through the scheduler on a
    // virtual clock, with three 40 ms process stalls for the hitches, or in real time on the flight thread with the
    // stick reports from an input thread. Both log through the ring and the flush worker.
    internal static class ScriptedFlight
    {
        public const ulong Seed = 0x450F;

        const long StartUs = 1000000;
        const long TickUs = 250;
        const long StallUs = 40000;
        static readonly long[] StallsAtUs = { StartUs + 30000000, StartUs + 60500000, StartUs + 90250000 };

        public sealed class Recorded
        {
            public LogEnd Counts;
            public long Bytes, HeaderBytes;
            public ulong Seed, DroneHash;
            public double WallSeconds;
            public Exception Fault;
        }

        sealed class VirtualClock : IHostClock
        {
            public long Now;
            public long NowUs() => Now;
        }

        /// <summary>The worlds a replay can open by the header's id.</summary>
        public static FlightWorld OpenWorld(string id) => id == "floor" ? FlightWorld.Floor() : null;

        /// <summary>The repository's drone.json, found from the working directory upwards; null when there is none.</summary>
        public static string RepositoryDrone()
        {
            for (string dir = Directory.GetCurrentDirectory(); dir != null; dir = Path.GetDirectoryName(dir))
            {
                string path = Path.Combine(dir, "assets-src", "drone", "drone.json");
                if (File.Exists(path)) return path;
            }
            return null;
        }

        /// <summary>DroneSpec.ContentHash of the file, or 0 with problem set when it cannot be read or is not valid.</summary>
        public static ulong DroneHash(string path, out string problem)
        {
            try
            {
                problem = null;
                return DroneSpec.Parse(File.ReadAllText(path)).ContentHash;
            }
            catch (DroneSpecException e)
            {
                problem = e.Message;
            }
            catch (IOException e)
            {
                problem = "drone.json cannot be read: " + e.Message;
            }
            return 0;
        }

        // The pilot's sticks t µs into the flight, at the TX12's resolution (1 µs of a 1000–2000 µs channel): armed from
        // 0.2 s with the throttle low (Betaflight's rule), the throttle up to about hover over 2 s from 0.5 s and then
        // cycling around it, a roll weave, pitch reversals with a full sharp reverse every 15 s, a slow yaw.
        static StickSample Stick(long tUs, long hostUs)
        {
            double t = tUs * 1e-6;
            double throttle = t < 0.5 ? 0 : t < 2.5 ? 0.235 * (t - 0.5) : 0.47 + 0.15 * Triangle(t / 7);
            double roll = 0.6 * Math.Sin(2 * Math.PI * 0.5 * t) + (t % 10 < 0.3 ? 0.4 : 0);
            double pitch = t % 15 >= 7 && t % 15 < 7.5 ? -1 : (int)(t / 3) % 2 == 0 ? 0.4 : -0.4;
            double yaw = 0.3 * Math.Sin(2 * Math.PI * 0.2 * t);
            return new StickSample
            {
                HostUs = hostUs,
                Roll = Axis(roll),
                Pitch = Axis(pitch),
                Yaw = Axis(yaw),
                Throttle = Math.Round(Math.Clamp(throttle, 0, 1) * 1000) / 1000,
                Arm = t >= 0.2,
            };
        }

        static double Axis(double v) => Math.Round(Math.Clamp(v, -1, 1) * 500) / 500;

        static double Triangle(double x) => 4 * Math.Abs(x - Math.Floor(x + 0.5)) - 1;

        public static Recorded Fly(string path, string dronePath, long steps, bool realtime, WindLevel wind = WindLevel.Windy)
        {
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(dronePath));
            Loadout loadout = spec.Nominal;
            var setup = new FlightSetup
            {
                Seed = Seed,
                World = OpenWorld("floor"),
                Mass = spec.BuildMass(loadout),
                Propulsion = spec.BuildPropulsion(),
                Aero = spec.BuildAero(),
                Wind = WindSetup.Of(wind),
                StartPosition = new Double3(0, 300, 0),
            };
            var core = new FlightCore(setup);
            LogHeader header = LogHeader.For(setup, core);
            header.Build = Host.Build;
            header.Isa = Host.Isa;
            header.WorldId = "floor";
            header.SetDrone(spec, loadout);

            var inbox = new StickInbox();
            var poses = new PoseBuffer();
            var wall = Stopwatch.StartNew();
            var recorder = new FlightRecorder(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16), header);
            Exception fault;
            if (realtime)
                fault = RealTime(core, inbox, poses, recorder, steps);
            else
                fault = Virtual(core, inbox, poses, recorder, steps);
            recorder.Close(inbox.Dropped);
            return new Recorded
            {
                Counts = recorder.Counts,
                Bytes = new FileInfo(path).Length,
                HeaderBytes = header.Length,
                Seed = Seed,
                DroneHash = header.DroneHash,
                WallSeconds = wall.Elapsed.TotalSeconds,
                Fault = fault ?? recorder.Fault,
            };
        }

        static Exception Virtual(FlightCore core, StickInbox inbox, PoseBuffer poses, FlightRecorder recorder, long steps)
        {
            var clock = new VirtualClock { Now = StartUs };
            var scheduler = new FlightScheduler(core, inbox, poses, clock, recorder);
            scheduler.Begin(StartUs);
            long report = StartUs - 2000;
            for (int i = 0, t = 0; core.State.Step < steps; t++)
            {
                long now = StartUs + t * TickUs;
                clock.Now = now;
                for (; report <= now; i++)
                {
                    inbox.TryWrite(Stick(report - StartUs, report));
                    report += 3700 + i * 131 % 600;
                }
                if (!Stalled(now)) scheduler.RunDue(now);
            }
            return null;
        }

        static bool Stalled(long now)
        {
            foreach (long at in StallsAtUs)
                if (now >= at && now < at + StallUs)
                    return true;
            return false;
        }

        static Exception RealTime(FlightCore core, StickInbox inbox, PoseBuffer poses, FlightRecorder recorder, long steps)
        {
            using (Host.FineTimer())
            {
                IHostClock clock = HostClock.Instance;
                var scheduler = new FlightScheduler(core, inbox, poses, clock, recorder);
                long start = clock.NowUs() + 20000;
                bool stop = false;
                var input = new Thread(() =>
                {
                    while (!Volatile.Read(ref stop))
                    {
                        long now = clock.NowUs();
                        inbox.TryWrite(Stick(now - start, now));
                        Thread.Sleep(4);
                    }
                }) { Name = "scripted pilot", IsBackground = true };
                input.Start();
                scheduler.Start(start);
                while (core.State.Step < steps && scheduler.Fault == null)
                    Thread.Sleep(50);
                scheduler.Stop();
                Volatile.Write(ref stop, true);
                input.Join();
                return scheduler.Fault;
            }
        }
    }
}
