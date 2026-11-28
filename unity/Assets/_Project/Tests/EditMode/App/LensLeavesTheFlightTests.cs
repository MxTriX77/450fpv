using System;
using System.IO;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Tests.App
{
    /// <summary>
    /// The drops drawn leave the flight as it was (w21-rain-on-camera 1.2, D-126: the lens's water never feeds back): the same
    /// headless flight over the farmland in Severe rain and Windy, flown twice, once alone and once with the lens's water stepped
    /// on its published poses every 16 ms frame and drawn onto the feed's map, as FlightScene does. The two logs are the same byte
    /// for byte, and the drawn lens took the rain. Sunny keeps the lens dry and the map empty.
    /// </summary>
    public sealed class LensLeavesTheFlightTests
    {
        [Test, Timeout(600000)]
        public void AFlightsLogIsTheSameWithTheLensDrawn()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fpv450-lens-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                byte[] alone = Fly(dir, "alone", null, out string aloneNote);
                var lens = new LensView();
                byte[] drawn = Fly(dir, "drawn", lens, out string drawnNote);
                TestContext.WriteLine("alone: " + aloneNote);
                TestContext.WriteLine("with the lens drawn: " + drawnNote);
                Assert.That(alone.Length, Is.GreaterThan(100000), "a log of the flight");
                Assert.That(drawn, Is.EqualTo(alone), "the log is the same byte for byte with the lens drawn");
                Assert.That(lens.Water.Arrived, Is.GreaterThan(0), "the rain reached the drawn lens");
                Assert.That(lens.Map.Drops, Is.EqualTo(lens.Water.Drops.Length), "every drop on the glass is on the map");
                Assert.That(lens.Map.Wet, Is.EqualTo(lens.Water.Drops.Length > 0));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// Sunny (the flight's rate 0): a minute of fast flight into a headwind leaves the lens dry and the map empty.
        [Test]
        public void SunnyDrawsNothing()
        {
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
            var lens = new LensView();
            lens.Begin(1, spec, spec.BuildMass(spec.Nominal), 0, 0x450F);
            var poses = new PoseBuffer();
            double half = 12.5 * Math.PI / 180;
            var pitched = new Quat(Math.Sin(half), 0, 0, Math.Cos(half)); // 25° nose down
            PoseFrame Pose(long step) => new PoseFrame
            {
                Step = step, Position = new Double3(0, 10, 20 * step * FlightCore.Dt), Attitude = pitched, Velocity = new Double3(0, 0, 20),
                AirVelocityAtCamera = pitched.InverseRotate(new Double3(0, 0, -26)), Armed = true,
            };
            poses.Begin(0, Pose(0), default);
            bool changed = false;
            for (long s = 1; s <= 60000; s++)
            {
                poses.Publish(Pose(s), default);
                if (s % 16 == 0) changed |= lens.Advance(poses);
            }
            Assert.That(lens.Water.Arrived, Is.Zero);
            Assert.That(changed || lens.Map.Wet || lens.Map.Drops > 0 || lens.Map.Pearls > 0, Is.False, "nothing drawn");
        }

        static byte[] Fly(string dir, string name, LensView lens, out string note)
        {
            string root = AppData.Root;
            var clock = new ManualClock { Now = 1000000 };
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
            MaterialRow[] materials = PhysicsMaterials.Parse(File.ReadAllText(AppData.File(PhysicsMaterials.RelativePath)), out ulong materialsHash);
            SurfaceTable surfaces = SurfaceTable.Parse(File.ReadAllText(AppData.File(SurfaceTable.RelativePath)));
            TetherTable tether = TetherTable.Parse(File.ReadAllText(AppData.File(TetherTable.RelativePath)));
            var inputs = new SessionInputs
            {
                Drone = spec, Materials = materials, MaterialsHash = materialsHash, Surfaces = surfaces, Tether = tether,
                Settings = FcSettingsStore.PlayerDefault, LogFolder = Path.Combine(dir, name), Build = "lenstests",
                Reader = new RadioReader(() => null), KeyboardAllowed = true, Clock = clock, Threaded = false,
            };
            FlightWorlds.Farmland(root).ApplyTo(inputs);
            inputs.Choices = new FlightChoices { StartPoint = "village", Legs = true, Payload = 1.0, Wind = WindLevel.Windy, Rain = Rain.SevereRain, TimeOfDay = 12 };
            inputs.NewSeed = () => 0x1630;
            var session = new FlightSession(inputs);
            int frames = 0;
            try
            {
                void Run(int ms, KeyboardKeys keys)
                {
                    for (int i = 0; i < ms && session.State != SessionState.Ended; i++)
                    {
                        clock.Now += FlightCore.StepUs;
                        inputs.Reader.Pump(0);
                        session.Tick(keys);
                    }
                    if (lens == null || session.Flights == 0) return;
                    // FlightScene.Lens: begun from this flight's first pose, stepped every frame.
                    ref readonly PosePair pair = ref session.Poses.Read();
                    if (pair.StartUs < session.StartUs) return;
                    if (lens.Flight != session.Flights)
                        lens.Begin(session.Flights, spec, session.Setup.Mass, pair.Signals.RainRateMmPerHour, session.Seed);
                    lens.Advance(session.Poses);
                    frames++;
                }
                Run(10, KeyboardKeys.None);
                session.Start();
                Run(1000, KeyboardKeys.None);
                Run(100, KeyboardKeys.ArmToggle);
                Run(400, KeyboardKeys.None);
                IWorldQuery world = session.Setup.World.Query;
                var at = new XZ[1];
                var under = new GroundSample[1];
                var pilot = new HillsPilot(inputs.Settings, (x, z) =>
                {
                    at[0] = new XZ(x, z);
                    world.SampleGround(at, under);
                    return under[0].Height;
                });
                const int Frame = 16;
                while (session.Core.State.Step < 15000 && !session.Core.State.Crashed)
                {
                    PoseFrame pose = session.Poses.Read().Current;
                    Run(Frame, pilot.Next(pose, Frame * 1e-3));
                }
                PoseFrame end = session.Poses.Read().Current;
                note = $"{end.TimeS:0.0} s, at ({end.Position.X:0.0}, {end.Position.Y:0.0}, {end.Position.Z:0.0}), {pilot.Now}" +
                       (lens != null ? $"; the lens stepped {frames} frames to field {lens.Water.Field}: {lens.Water.Arrived} arrived, " +
                                       $"{lens.Water.Drops.Length} on the glass, {lens.Map.Pearls} trail pearls drawn" : "");
                Run(100, KeyboardKeys.ArmToggle);
                session.EndFlight();
                string log = session.LogPath;
                session.Dispose();
                session = null;
                return File.ReadAllBytes(log);
            }
            finally
            {
                session?.Dispose();
            }
        }

        sealed class ManualClock : IHostClock
        {
            public long Now;
            public long NowUs() => Now;
        }
    }
}
