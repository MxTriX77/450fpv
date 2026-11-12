using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Fpv450.App.Session;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;
using StartPoint = Fpv450.App.Session.StartPoint;

namespace Fpv450.Tests.Session
{
    // w3-farmland-ground 3.1: the player's flight over the farmland, with no scene. Its world is the farmland's MapWorld
    // (its 17 tiles, no objects until 1.2) from the village's start (D-092); and the spec's scenario "a flight on the hills
    // replays", flown headless on the development keyboard by HillsPilot (the smoke test's -smokehills flies the same pilot
    // in the player): two minutes over the hills, its log replayed bit for bit and read for the ground.
    public sealed class FarmlandFlightTests
    {
        static DroneSpec _spec;
        static MaterialRow[] _materials;
        static ulong _materialsHash;
        static SurfaceTable _surfaces;
        static TetherTable _tether;
        static FlightWorldChoice _farmland;
        string _dir;

        [OneTimeSetUp]
        public void Load()
        {
            _spec = DroneSpec.Parse(File.ReadAllText(RepositoryFile("assets-src/drone/drone.json")));
            _materials = PhysicsMaterials.Parse(File.ReadAllText(RepositoryFile(PhysicsMaterials.RelativePath)), out _materialsHash);
            _surfaces = SurfaceTable.Parse(File.ReadAllText(RepositoryFile(SurfaceTable.RelativePath)));
            _tether = TetherTable.Parse(File.ReadAllText(RepositoryFile(TetherTable.RelativePath)));
            _farmland = FlightWorlds.Farmland(RepositoryRoot());
        }

        [SetUp]
        public void MakeFolder()
        {
            _dir = Path.Combine(Path.GetTempPath(), "fpv450-farmland-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void RemoveFolder()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }

        // The farmland's world and its one start: the map package's terrain under the village's start, legs on standing on
        // it, legs off the launch stand placed on it.
        [Test]
        public void TheFarmlandFliesFromTheVillagesStartOnItsHeights()
        {
            Assert.That(_farmland.Id, Is.EqualTo(FlightWorlds.FarmlandMap), "the map id the log records, so replay reopens it");
            Assert.That(_farmland.Open().Query, Is.InstanceOf<MapWorld>());
            Assert.That(_farmland.Open().ContentHash, Is.EqualTo(MapWorld.Load(RepositoryRoot(), FlightWorlds.FarmlandMap).ContentHash));
            Assert.That(_farmland.StartPoints.Length, Is.EqualTo(1));
            StartPoint village = _farmland.StartPoints[0];
            Assert.That((village.Name, village.X, village.Z, village.Stand), Is.EqualTo(("village", 566.5, 569.0, false)));
            Assert.That(village.Yaw, Is.EqualTo(300 * Math.PI / 180).Within(1e-15), "heading 300°, at the nearest ruin");

            var under = new GroundSample[1];
            _farmland.Open().SampleGround(new[] { new XZ(village.X, village.Z) }, under);
            TestContext.WriteLine("the village's start: ground " + under[0].Height.ToString("0.0000") + " m, normal " + under[0].Normal.Y.ToString("0.000000"));
            Assert.That(under[0].Height, Is.EqualTo(37.89).Within(0.01), "the foot on the terrain at 37.89 m (w1-farmland-world design 6)");
            foreach (bool legs in new[] { true, false })
            {
                SessionInputs inputs = Inputs(new ManualClock());
                inputs.Choices = new FlightChoices { StartPoint = "track", Legs = legs }; // an older setup's start: the village's
                FlightSetup setup = SessionSetup.Build(inputs, 1, out _, out StartPoint start);
                Assert.That(start.Name, Is.EqualTo("village"));
                Assert.That((setup.StartPosition.X, setup.StartPosition.Z), Is.EqualTo((village.X, village.Z)));
                RuntimeObject[] objects = setup.RuntimeObjects ?? new RuntimeObject[0];
                if (legs)
                {
                    Assert.That(setup.StartPosition.Y, Is.EqualTo(under[0].Height + SessionSetup.Standing(setup.Contacts)).Within(1e-12), "on its skids on the terrain");
                    Assert.That(objects, Is.Empty, "legs on, no stand");
                    continue;
                }
                Assert.That(objects.Length, Is.EqualTo(1));
                Assert.That(objects[0].Name, Is.EqualTo(LaunchStand.Name));
                double y = double.PositiveInfinity;
                foreach (ShapeGeometry s in objects[0].Shapes) y = Math.Min(y, s.Center.Y);
                Assert.That(y, Is.GreaterThan(under[0].Height), "the stand stands on the terrain, not at the plane's 0");
            }
        }

        // The scenario: two minutes over the hills from the village's start, on the keyboard as the smoke test flies it (its
        // frames every 16 ms), legs on, 1 kg, Windy, the line paying out. It climbs, flies HillsPilot's route over the hills,
        // lands and is disarmed, with no crash; its log replays bit for bit, and has no tunnel and no body part past the
        // ground at any step (GroundCheck).
        [Test]
        public void AFlightOverTheHillsReplays()
        {
            var clock = new ManualClock { Now = 1000000 };
            SessionInputs inputs = Inputs(clock);
            inputs.Choices = new FlightChoices { StartPoint = "village", Legs = true, Payload = 1.0, Wind = WindLevel.Windy, Rain = Rain.Sunny, TimeOfDay = 12 };
            inputs.NewSeed = () => 0x3110;
            var session = new FlightSession(inputs);
            void Run(int ms, KeyboardKeys keys)
            {
                for (int i = 0; i < ms && session.State != SessionState.Ended; i++)
                {
                    clock.Now += FlightCore.StepUs;
                    inputs.Reader.Pump(0);
                    session.Tick(keys);
                }
            }
            try
            {
                Run(10, KeyboardKeys.None);
                session.Start();
                Assert.That(session.Input, Is.EqualTo(InputSource.Keyboard));
                Run(1000, KeyboardKeys.None);
                Run(100, KeyboardKeys.ArmToggle);
                Run(400, KeyboardKeys.None);
                Assert.That(session.Core.State.Armed, "armed by the keyboard's switch");

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
                while (pilot.Now != HillsPilot.Phase.Done && session.Core.State.Step < 300000 && !session.Core.State.Crashed)
                {
                    PoseFrame pose = session.Poses.Read().Current;
                    Run(Frame, pilot.Next(pose, Frame * 1e-3));
                }
                PoseFrame end = session.Poses.Read().Current;
                TestContext.WriteLine("HillsPilot: " + pilot.Now + " at t = " + end.TimeS.ToString("0.0") + " s, (" + end.Position.X.ToString("0.0") + ", "
                    + end.Position.Y.ToString("0.0") + ", " + end.Position.Z.ToString("0.0") + "), the ground under its cruise " + pilot.LowestGround.ToString("0.0")
                    + "–" + pilot.HighestGround.ToString("0.0") + " m, " + end.FiberPaidOut.ToString("0.0") + " m of line out");
                Assert.That(session.Core.State.Crashed, Is.False, "no crash");
                Assert.That(pilot.Now, Is.EqualTo(HillsPilot.Phase.Done), "flew the route and landed");
                Assert.That(pilot.HighestGround - pilot.LowestGround, Is.GreaterThan(15), "over the hills");
                Run(100, KeyboardKeys.ArmToggle);
                Run(500, KeyboardKeys.None);
                Assert.That(session.Core.State.Armed, Is.False, "disarmed on the ground");
                session.EndFlight();

                PostFlight summary = session.ReadSummary();
                TestContext.WriteLine(summary.Log.Seconds.ToString("0.0") + " s: " + string.Join(" | ", summary.Lines()));
                Assert.That(summary.Log.Complete, summary.Log.CutReason);
                Assert.That(summary.HasCrash, Is.False);
                Assert.That(summary.Log.Seconds, Is.GreaterThanOrEqualTo(120), "two minutes");
                Assert.That(summary.Log.Header.WorldId, Is.EqualTo(FlightWorlds.FarmlandMap));

                ReplayResult replay;
                using (var f = new FileStream(session.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    replay = FlightReplay.Run(f, id => id == _farmland.Id ? _farmland.Open() : null, _spec.ContentHash, surfacesHash: _surfaces.Hash,
                        tetherHash: _tether.ContentHash, materialsHash: _materialsHash);
                TestContext.WriteLine(replay.Message);
                Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);

                GroundCheck.Result g = GroundCheck.Read(session.LogPath, _farmland.Open().Query);
                TestContext.WriteLine(g.ToString());
                Assert.That(g.Tunnels, Is.EqualTo(0), "no tunnel event");
                Assert.That(g.BodyDepth, Is.LessThanOrEqualTo(0), "no body part past the ground");
            }
            finally
            {
                session.Dispose();
            }
        }

        SessionInputs Inputs(ManualClock clock)
        {
            var inputs = new SessionInputs
            {
                Drone = _spec,
                Materials = _materials,
                MaterialsHash = _materialsHash,
                Surfaces = _surfaces,
                Tether = _tether,
                Settings = FcSettingsStore.PlayerDefault,
                LogFolder = Path.Combine(_dir, "flights"),
                Build = "farmlandtests",
                Reader = new RadioReader(() => null),
                KeyboardAllowed = true,
                Clock = clock,
                Threaded = false,
            };
            _farmland.ApplyTo(inputs);
            return inputs;
        }

        sealed class ManualClock : IHostClock
        {
            public long Now;
            public long NowUs() => Now;
        }

        static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(RepositoryFile("maps/farmland/map.json")), "..", ".."));

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
            throw new FileNotFoundException(relative);
        }
    }
}
