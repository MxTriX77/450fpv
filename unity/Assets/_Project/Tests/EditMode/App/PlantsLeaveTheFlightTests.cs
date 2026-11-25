using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
    /// The plants' drawing leaves the flight as it was (w16-plants-in-the-air 1.3; PLT-04, the sim core's determinism): the same
    /// flight, headless on the development keyboard (the seed, the keys, the clock), over the farmland in Windy from the village's
    /// start, flown twice: once alone, once with the plants' CPU side reading it all the while on three threads of their own as
    /// PlantField's helpers do (the wash grids ticked from its published poses on its wind field and world, and the wind's mean
    /// on a grid round the drone, each frame's samples beside them; the samples that read the wash grids run on the wash's thread, as PlantField exports them). The two logs are the same byte for byte. (The GPU side draws
    /// only; nothing it writes is read by anything but the plants' shaders.)
    /// </summary>
    public sealed class PlantsLeaveTheFlightTests
    {
        [Test, Timeout(600000)]
        public void AFlightsLogIsTheSameWithThePlantsDrawn()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fpv450-plants-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                byte[] alone = Fly(dir, "alone", plants: false, out string aloneNote);
                byte[] drawn = Fly(dir, "drawn", plants: true, out string drawnNote);
                TestContext.WriteLine("alone: " + aloneNote);
                TestContext.WriteLine("with the plants: " + drawnNote);
                Assert.That(alone.Length, Is.GreaterThan(100000), "a log of the flight");
                Assert.That(drawn, Is.EqualTo(alone), "the log is the same byte for byte with the plants drawn");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        static byte[] Fly(string dir, string name, bool plants, out string note)
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
                Settings = FcSettingsStore.PlayerDefault, LogFolder = Path.Combine(dir, name), Build = "plantstests",
                Reader = new RadioReader(() => null), KeyboardAllowed = true, Clock = clock, Threaded = false,
            };
            FlightWorlds.Farmland(root).ApplyTo(inputs);
            inputs.Choices = new FlightChoices { StartPoint = "village", Legs = true, Payload = 1.0, Wind = WindLevel.Windy, Rain = Rain.Sunny, TimeOfDay = 12 };
            inputs.NewSeed = () => 0x1630;
            var session = new FlightSession(inputs);
            var stop = new ManualResetEventSlim();
            var threads = new List<Thread>();
            int ticks = 0, means = 0;
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
                }
                Run(10, KeyboardKeys.None);
                session.Start();
                if (plants) threads = Plants(session, inputs, stop, () => ticks++, () => means++);
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
                stop.Set();
                foreach (Thread t in threads) t.Join();
                PoseFrame end = session.Poses.Read().Current;
                note = $"{end.TimeS:0.0} s, at ({end.Position.X:0.0}, {end.Position.Y:0.0}, {end.Position.Z:0.0}), {pilot.Now}" +
                       (plants ? $"; the plants' threads ran {ticks} wash ticks and {means} means" : "");
                Run(100, KeyboardKeys.ArmToggle);
                session.EndFlight();
                string log = session.LogPath;
                session.Dispose();
                session = null;
                return File.ReadAllBytes(log);
            }
            finally
            {
                stop.Set();
                foreach (Thread t in threads) t.Join();
                session?.Dispose();
            }
        }

        // The plants' CPU side on the flight, as PlantField's helpers run it: the wash grids from its poses, and the wind's mean (and
        // a frame's samples) round the drone, until stopped.
        static List<Thread> Plants(FlightSession session, SessionInputs inputs, ManualResetEventSlim stop, Action ticked, Action meant)
        {
            FlightCore core = session.Core;
            FlightSetup setup = session.Setup;
            double air = setup.Tether.Number("fiber.air_density_kg_m3");
            PlantMechanics[] bands = PlantBands.Mechanics(k => core.Contacts.Cover(k), air);
            var grass = PlantBands.Washed.Select(b => bands[b]).ToArray();
            var motors = setup.Propulsion.Motors.Select(m => m.Position).ToArray();
            double radius = setup.Propulsion.Props.Diameter / 2;
            Moisture moisture = SurfaceTable.MoistureOf(setup.Rain);
            var washAir = new PlantAir(core.Wind);
            var fine = new PlantWash(setup.Tether, radius, motors, core.World.Query, washAir, grass, inputs.Surfaces, moisture, 32, 0.05);
            var coarse = new PlantWash(setup.Tether, radius, motors, core.World.Query, washAir, grass, inputs.Surfaces, moisture, 32, 0.15);
            // The wash's two helpers, as PlantField's: the fine grid on one, the coarse on the other, at once.
            var fineWash = new Thread(() =>
            {
                while (!stop.IsSet)
                {
                    long before = fine.LastTick;
                    fine.Advance(session.Poses);
                    if (fine.LastTick != before) ticked();
                    for (int i = 0; i < 64; i++) fine.DustAt(i * 0.01, 0, out _);
                    Thread.Sleep(1);
                }
            }) { IsBackground = true, Name = "plants: the fine wash (test)" };
            var coarseWash = new Thread(() =>
            {
                while (!stop.IsSet)
                {
                    coarse.Advance(session.Poses);
                    Thread.Sleep(1);
                }
            }) { IsBackground = true, Name = "plants: the coarse wash (test)" };
            var mean = new Thread(() =>
            {
                var plants = new PlantAir(core.Wind);
                var xz = new XZ[1];
                var g = new GroundSample[1];
                int k = 0;
                while (!stop.IsSet)
                {
                    session.Poses.TryRead(session.Poses.LatestStep, out PoseFrame pose);
                    plants.Begin(pose.TimeS);
                    double x = pose.Position.X - 40 + (k * 7.3) % 80, z = pose.Position.Z - 40 + (k * 3.1) % 80;
                    xz[0] = new XZ(x, z);
                    core.World.Query.SampleGround(xz, g);
                    if (g[0].Height > double.NegativeInfinity)
                    {
                        int b = k % PlantBands.Count;
                        var basePoint = new Double3(x, g[0].Height + PlantBands.Elevation(b), z);
                        plants.Mean(bands[b], basePoint, g[0].Height, out _);
                        plants.Wind(bands[b], basePoint, g[0].Height, out _);
                        meant();
                    }
                    k++;
                }
            }) { IsBackground = true, Name = "plants: the wind's mean (test)" };
            fineWash.Start();
            coarseWash.Start();
            mean.Start();
            return new List<Thread> { fineWash, coarseWash, mean };
        }

        sealed class ManualClock : IHostClock
        {
            public long Now;
            public long NowUs() => Now;
        }
    }
}
