using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;

namespace Fpv450.Replay
{
    // The scripted flight that exercises the log end to end (physics-log, "Replay matches the flight"): drone.json at
    // its nominal loadout with its Betaflight 4.5 controller and propulsion (1.4), its drag in a stand-in wind (1.5;
    // Windy unless asked otherwise) and its contacts (1.6; with surfaces.json's table, p7a-ground 1.1, which the header
    // records and the replay checks), from rest on the stub's floor. A pilot watching the published
    // pose (SimPilot, as a pilot watches the feed) arms, climbs to 20 m, flies a circuit with a sharp reverse every 15 s,
    // comes back, lands on the slow glide path with the sticks held, cuts the throttle after the touch and disarms. It is
    // flown either through the scheduler on a virtual clock, with three 40 ms process stalls for the hitches, or in real
    // time on the flight thread with the stick reports from an input thread. Both log through the ring and the flush
    // worker; the replay only needs the logged sticks.
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
            public string Landing = "";
        }

        sealed class VirtualClock : IHostClock
        {
            public long Now;
            public long NowUs() => Now;
        }

        /// <summary>The worlds a replay can open by the header's id.</summary>
        public static FlightWorld OpenWorld(string id) => id == "floor" ? FlightWorld.Floor() : null;

        /// <summary>The repository's drone.json, found from the working directory upwards; null when there is none.</summary>
        public static string RepositoryDrone() => Repository(Path.Combine("assets-src", "drone", "drone.json"));

        /// <summary>The repository's surfaces.json (p7a-ground 1.1), found the same way; null when there is none.</summary>
        public static string RepositorySurfaces() => Repository(Path.Combine("assets-src", "surfaces.json"));

        static string Repository(string relative)
        {
            for (string dir = Directory.GetCurrentDirectory(); dir != null; dir = Path.GetDirectoryName(dir))
            {
                string path = Path.Combine(dir, relative);
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

        /// <summary>SurfaceTable.Hash of the file, or 0 with problem set when it cannot be read or is not valid.</summary>
        public static ulong SurfacesHash(string path, out string problem)
        {
            try
            {
                problem = null;
                return SurfaceTable.Parse(File.ReadAllText(path)).Hash;
            }
            catch (FormatException e)
            {
                problem = e.Message;
            }
            catch (IOException e)
            {
                problem = "surfaces.json cannot be read: " + e.Message;
            }
            return 0;
        }

        /// <summary>
        /// The pilot: SimPilot on the published pose, with a plan. Disarmed on the floor to 0.2 s, armed with the throttle
        /// low to 0.5 s; up to 20 m by 12 s; then a 30 m circuit at 8 m/s, its target reversed for 0.6 s every 15 s (a full
        /// sharp reverse); from 80 s back over the start at 20 m; from 92 s down at 2 m/s, and at 0.5 m/s from 3 m; with
        /// the skids 0.1 m up the sticks are held (roll, pitch and yaw centred) through the touch; 0.5 s after the legs
        /// first compress the throttle is cut, and 2 s later the switch goes off. TX12 resolution on every axis.
        /// </summary>
        sealed class Pilot
        {
            const double Cruise = 8, Radius = 30, Height = 20;
            readonly SimPilot _p;
            readonly double _stand;
            StickSample _held;
            bool _holding;
            double _slowFrom = -1, _slowY, _touchAt = -1;

            public Pilot(FlightCore core, double stand)
            {
                _p = new SimPilot(core.Controller, new Double3(0, stand, 0)) { Period = 1 };
                _stand = stand;
            }

            public string Landing = "";

            public StickSample Sticks(double t, in PoseFrame pose, long hostUs)
            {
                if (t < 0.5) return new StickSample { HostUs = hostUs, Arm = t >= 0.2 };
                double clearance = pose.Position.Y - _stand;
                if (_touchAt < 0 && _holding && (pose.LegCompression.RearRight > 0 || pose.LegCompression.FrontRight > 0
                    || pose.LegCompression.RearLeft > 0 || pose.LegCompression.FrontLeft > 0))
                {
                    _touchAt = t;
                    Landing = $"touched at {t:F3} s at {-pose.Velocity.Y:F3} m/s down";
                }
                StickSample s;
                if (_holding) s = _held;
                else
                {
                    if (t < 12)
                    {
                        _p.Target = new Double3(0, _stand + Math.Min(Height, 2 * (t - 0.5)), 0);
                    }
                    else if (t < 80)
                    {
                        double a = (t - 12) * Cruise / Radius;
                        Double3 along = new Double3(Math.Cos(a), 0, Math.Sin(a)) * Cruise;
                        if ((t - 12) % 15 >= 7 && (t - 12) % 15 < 7.6) along = along * -1;
                        _p.HoldVelocity = true;
                        _p.TargetVelocity = along;
                        _p.Target = new Double3(0, _stand + Height, 0);
                    }
                    else
                    {
                        _p.HoldVelocity = false;
                        double y = t < 92 ? Height : Math.Max(0, Height - 2 * (t - 92));
                        if (_slowFrom < 0 && t >= 92 && clearance < 3)
                        {
                            _slowFrom = t;
                            _slowY = clearance;
                        }
                        if (_slowFrom >= 0) y = _slowY - 0.8 - 0.5 * (t - _slowFrom);
                        _p.Target = new Double3(0, _stand + y, 0);
                    }
                    s = _p.Sticks(pose, hostUs);
                    if (_slowFrom >= 0 && clearance < 0.1 && pose.Velocity.Y < 0)
                    {
                        _holding = true;
                        _held = s;
                        _held.Roll = _held.Pitch = _held.Yaw = 0;
                        s = _held;
                    }
                }
                if (_touchAt >= 0 && t >= _touchAt + 0.5) s.Throttle = 0;
                if (_touchAt >= 0 && t >= _touchAt + 2.5) s.Arm = false;
                s.HostUs = hostUs;
                s.Roll = Axis(s.Roll);
                s.Pitch = Axis(s.Pitch);
                s.Yaw = Axis(s.Yaw);
                s.Throttle = Math.Round(Math.Clamp(s.Throttle, 0, 1) * 1000) / 1000;
                return s;
            }
        }

        static double Axis(double v) => Math.Round(Math.Clamp(v, -1, 1) * 500) / 500;

        public static Recorded Fly(string path, string dronePath, long steps, bool realtime, WindLevel wind = WindLevel.Windy, bool wakes = false,
            RainLevel rain = RainLevel.Sunny, bool canopy = false)
        {
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(dronePath));
            string materialsPath = Repository(Path.Combine("assets-src", "physics_materials.json"))
                ?? throw new FileNotFoundException("assets-src/physics_materials.json was not found");
            MaterialRow[] materials = PhysicsMaterials.Parse(File.ReadAllText(materialsPath), out ulong materialsHash);
            SurfaceTable surfaces = SurfaceTable.Parse(File.ReadAllText(RepositorySurfaces()
                ?? throw new FileNotFoundException("assets-src/surfaces.json was not found")));
            Loadout loadout = spec.Nominal;
            ContactSpec contacts = spec.BuildContacts(loadout, materials, materialsHash, surfaces);
            double stand = 0; // the body origin's height standing on the floor, the skids unloaded
            foreach (ContactShape s in contacts.Shapes)
                if (s.Role == ContactRole.Support) stand = Math.Max(stand, s.Radius - s.A.Y);
            WindSetup air = WindSetup.Of(wind);
            // --wakes (p7c-wind 1.5): the wake grid's synthetic layout (a maple row along X through the start, with a gap,
            // and a solid house), so the replay rebuilds the grid and its worker's handovers from the header. --canopy: a
            // wooded strip as the wind harness's (16.6 m tall, 141 m wide, λ 0.3) through the start, along X, so the replay
            // rebuilds the canopy's flow from the header.
            if (wakes) air.Wakes = new WakeLayout { OriginX = -300, OriginZ = -300, NX = 300, NZ = 300, Volumes = Fpv450.Sim.Tests.WakeGridFixture.Volumes() };
            if (canopy)
                air.Canopy = new CanopyStrip
                {
                    CentreX = 0, CentreZ = 0, AxisDeg = 90, Length = 1000, Width = 141, Height = 16.6, FrontalAreaIndex = 0.3,
                };
            var setup = new FlightSetup
            {
                Seed = Seed,
                World = OpenWorld("floor"),
                Mass = spec.BuildMass(loadout),
                Propulsion = spec.BuildPropulsion(),
                Aero = spec.BuildAero(),
                Contacts = contacts,
                Wind = air,
                Asymmetry = spec.BuildAsymmetry(), // a pilot's flight: the motors as this seed drew them (p7b-airframe 1.1)
                VortexRing = spec.BuildVortexRing(), // and the rotors' vortex rings (1.2)
                GroundEffect = spec.BuildGroundEffect(), // and their ground and ceiling effect and recirculation (1.3)
                Flapping = spec.BuildFlapping(), // and their edgewise-flow moments (1.3c)
                Modes = spec.BuildModes(), // and the airframe's modes (1.4)
                StartPosition = new Double3(0, stand, 0),
                Rain = rain,
                CameraPosition = spec.CameraPosition,
            };
            var core = new FlightCore(setup);
            LogHeader header = LogHeader.For(setup, core);
            header.Build = Host.Build;
            header.Isa = Host.Isa;
            header.WorldId = "floor";
            header.SetDrone(spec, loadout);

            var inbox = new StickInbox();
            var poses = new PoseBuffer();
            var pilot = new Pilot(core, stand);
            var wall = Stopwatch.StartNew();
            var recorder = new FlightRecorder(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16), header);
            Exception fault;
            if (realtime)
                fault = RealTime(core, inbox, poses, recorder, steps, pilot);
            else
                fault = Virtual(core, inbox, poses, recorder, steps, pilot);
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
                Landing = pilot.Landing + $"; ends at {core.State.Position.Y - stand:F4} m over standing, {core.Trace.ContactsTouching} touching pairs, "
                    + $"{core.Contacts.ImpactCount} impacts, armed {core.State.Armed}, crashed {core.State.Crashed}",
            };
        }

        static Exception Virtual(FlightCore core, StickInbox inbox, PoseBuffer poses, FlightRecorder recorder, long steps, Pilot pilot)
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
                    inbox.TryWrite(pilot.Sticks((report - StartUs) * 1e-6, poses.Read().Current, report));
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

        static Exception RealTime(FlightCore core, StickInbox inbox, PoseBuffer poses, FlightRecorder recorder, long steps, Pilot pilot)
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
                        inbox.TryWrite(pilot.Sticks((now - start) * 1e-6, poses.Read().Current, now));
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
