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

        // HillsPilot's route clears the farmland's objects (solid since 1.2). Its line over the ground runs from the village's
        // start toward each waypoint until within Reach of it, where the pilot turns to the next; from the line's end it brakes
        // and lands on the way to the last waypoint (the landing run). Within RouteClearance of the line no placed shape stands
        // higher than RouteBand over the ground under it: every crown, trunk and ruin is at least RouteClearance off, and the
        // bushes it crosses are at least Height − RouteBand under the cruise. Within RouteClearance of the start (the climb)
        // and of the landing run there is no shape at all. Each shape is taken at its world bounds and the line every Step, so
        // a margin is at most Step / 2 under its figure.
        [Test]
        public void TheHillsRouteClearsTheFarmlandsObjects()
        {
            const double Step = 0.25;
            var world = (MapWorld)_farmland.Open().Query;
            StartPoint village = _farmland.StartPoints[0];
            var at = new List<XZ> { new XZ(village.X, village.Z) };
            void Walk(XZ from, double toX, double toZ, double length)
            {
                double dx = toX - from.X, dz = toZ - from.Z, d = Math.Sqrt(dx * dx + dz * dz);
                int n = (int)Math.Ceiling(length / Step);
                for (int i = 1; i <= n; i++) at.Add(new XZ(from.X + dx / d * length * i / n, from.Z + dz / d * length * i / n));
            }
            foreach ((double wx, double wz) in HillsPilot.Route)
            {
                XZ from = at[at.Count - 1];
                double d = Math.Sqrt((wx - from.X) * (wx - from.X) + (wz - from.Z) * (wz - from.Z));
                if (d > HillsPilot.Reach) Walk(from, wx, wz, d - HillsPilot.Reach);
            }
            int line = at.Count; // the line's samples; the landing run's follow
            (double lastX, double lastZ) = HillsPilot.Route[HillsPilot.Route.Length - 1];
            Walk(at[line - 1], lastX, lastZ, HillsPilot.Reach);
            var ground = new GroundSample[at.Count];
            world.SampleGround(at.ToArray(), ground);
            double low = double.PositiveInfinity, high = double.NegativeInfinity, length = 0;
            for (int i = 0; i < line; i++)
            {
                low = Math.Min(low, ground[i].Height);
                high = Math.Max(high, ground[i].Height);
                if (i > 0) length += Math.Sqrt((at[i].X - at[i - 1].X) * (at[i].X - at[i - 1].X) + (at[i].Z - at[i - 1].Z) * (at[i].Z - at[i - 1].Z));
            }

            // Each object's shapes' bounds; an object whose bounds are farther than twice the margin from every sample is passed.
            double crossed = double.PositiveInfinity, ends = double.PositiveInfinity; // the nearest standing shape, the nearest to the climb or the landing
            string crossedBy = "none", endsBy = "none";
            var bounds = new List<(Double3 Lo, Double3 Hi)>();
            for (int obj = 0; obj < world.PlacedObjects; obj++)
            {
                bounds.Clear();
                var all = (Lo: new Double3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity), Hi: new Double3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity));
                for (int s = 0; world.Geometry(obj, s, out ShapeGeometry g); s++)
                {
                    WorldBounds(g, out Double3 lo, out Double3 hi);
                    bounds.Add((lo, hi));
                    all = (new Double3(Math.Min(all.Lo.X, lo.X), Math.Min(all.Lo.Y, lo.Y), Math.Min(all.Lo.Z, lo.Z)), new Double3(Math.Max(all.Hi.X, hi.X), Math.Max(all.Hi.Y, hi.Y), Math.Max(all.Hi.Z, hi.Z)));
                }
                double near = double.PositiveInfinity;
                foreach (XZ p in at) near = Math.Min(near, Across(all.Lo, all.Hi, p));
                if (near > 2 * RouteClearance) continue;
                for (int s = 0; s < bounds.Count; s++)
                    for (int i = 0; i < at.Count; i++)
                    {
                        (Double3 lo, Double3 hi) = bounds[s];
                        double d = Across(lo, hi, at[i]);
                        bool standing = i < line && hi.Y > ground[i].Height + RouteBand && d < crossed, end = (i == 0 || i >= line - 1) && d < ends;
                        if (!standing && !end) continue;
                        string name = world.ObjectName(obj) + " " + obj + "/" + s + " (its top " + (hi.Y - ground[i].Height).ToString("0.0") + " m over the ground there)";
                        if (standing)
                        {
                            crossed = d;
                            crossedBy = name;
                        }
                        if (end)
                        {
                            ends = d;
                            endsBy = name;
                        }
                    }
            }
            TestContext.WriteLine("HillsPilot's line: " + length.ToString("0") + " m over ground of " + low.ToString("0.0") + "–" + high.ToString("0.0") + " m; the nearest shape over "
                + RouteBand + " m: " + crossed.ToString("0.00") + " m across, " + crossedBy + "; the nearest of any height to the climb or the landing run: " + ends.ToString("0.00") + " m, " + endsBy);
            Assert.That(crossed, Is.GreaterThanOrEqualTo(RouteClearance), "no crown, trunk or ruin near the line: " + crossedBy);
            Assert.That(ends, Is.GreaterThanOrEqualTo(RouteClearance), "nothing by the climb or the landing: " + endsBy);
            Assert.That(high - low, Is.GreaterThan(15), "over the hills");
        }

        /// <summary>HillsPilot's route's margin across its line, m, and the height over the ground below which a shape may stand
        /// within it, m (1.5 m under the cruise's 5).</summary>
        const double RouteClearance = 12, RouteBand = 3.5;

        // How far a point is across the ground from bounds, m: 0 inside their x–z rectangle.
        static double Across(Double3 lo, Double3 hi, XZ p)
        {
            double ex = Math.Max(0, Math.Max(lo.X - p.X, p.X - hi.X)), ez = Math.Max(0, Math.Max(lo.Z - p.Z, p.Z - hi.Z));
            return Math.Sqrt(ex * ex + ez * ez);
        }

        // A shape's world bounds, as the world bounds it (a cylinder's are its capsule's).
        static void WorldBounds(in ShapeGeometry s, out Double3 lo, out Double3 hi)
        {
            Double3 e;
            switch (s.Kind)
            {
                case ShapeKind.Box:
                {
                    Double3 h = s.HalfExtents;
                    e = new Double3(Math.Abs(s.AxisX.X) * h.X + Math.Abs(s.AxisY.X) * h.Y + Math.Abs(s.AxisZ.X) * h.Z,
                        Math.Abs(s.AxisX.Y) * h.X + Math.Abs(s.AxisY.Y) * h.Y + Math.Abs(s.AxisZ.Y) * h.Z,
                        Math.Abs(s.AxisX.Z) * h.X + Math.Abs(s.AxisY.Z) * h.Y + Math.Abs(s.AxisZ.Z) * h.Z);
                    break;
                }
                case ShapeKind.Sphere:
                    e = new Double3(s.Radius, s.Radius, s.Radius);
                    break;
                default:
                    e = new Double3(Math.Abs(s.AxisY.X) * s.HalfLength + s.Radius, Math.Abs(s.AxisY.Y) * s.HalfLength + s.Radius, Math.Abs(s.AxisY.Z) * s.HalfLength + s.Radius);
                    break;
            }
            lo = s.Center - e;
            hi = s.Center + e;
        }

        // The scenario: two minutes over the hills from the village's start, on the keyboard as the smoke test flies it (its
        // frames every 16 ms), legs on, 1 kg, Calm, the line paying out. It climbs, flies HillsPilot's route over the hills,
        // lands and is disarmed, with no crash; its log replays bit for bit, and has no tunnel and no body part past the
        // ground at any step (GroundCheck), and the drone meets no placed object (PlacedObjectsMet). With 1.2's objects it
        // costs 71 s in .NET Release and 251 s in Unity's EditMode (Mono), past the runner's default of 180 s. Calm since
        // model 99 (w15-player-physics 1.1): in Windy the line laid behind the fourth leg lies over Pine006_01 (object 207, a
        // pine under 3 m, 13 m downwind of the drone's line), slips over its twigs and wears through there at 78 s (82 s with
        // the legs drawn), 12.6 N at the break, so the failsafe drops the drone at 1:19.7. That is the line's physics, not the
        // ground's or the replay's this scenario holds; in Calm the line lies still and the route is flown in 124 s.
        [Test, Timeout(600000)]
        public void AFlightOverTheHillsReplays()
        {
            var clock = new ManualClock { Now = 1000000 };
            SessionInputs inputs = Inputs(clock);
            inputs.Choices = new FlightChoices { StartPoint = "village", Legs = true, Payload = 1.0, Wind = WindLevel.Calm, Rain = Rain.Sunny, TimeOfDay = 12 };
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

                (int touches, string first, double closest, string by) = PlacedObjectsMet(session.LogPath, _farmland.Open().Query);
                TestContext.WriteLine("the placed objects: " + touches + " contacts, strikes or impacts (the first: " + first + "); the closest a shape came to the drone's centre: "
                    + closest.ToString("0.00") + " m, " + by);
                Assert.That(touches, Is.EqualTo(0), "no placed object met: " + first);
                // The route leaves a bush 1.5 m under the cruise's aim; less half a metre for the height's error.
                Assert.That(closest, Is.GreaterThan(HillsPilot.Height - RouteBand - 0.5), "every placed shape at least a metre off the drone's centre");
            }
            finally
            {
                session.Dispose();
            }
        }

        // A flight's meetings with the placed objects, from its log: the drone's contacts, strikes, impacts and crashes on one
        // (not the ground's, not the line's), and the closest any placed shape came to the drone's centre over its steps (the
        // world's StaticContacts for a point, within Near).
        static (int Touches, string First, double Closest, string By) PlacedObjectsMet(string logPath, IWorldQuery world)
        {
            const double Near = 6;
            var found = new StaticContact[4096];
            int touches = 0;
            string first = "none", by = "none within " + Near + " m";
            double closest = double.PositiveInfinity;
            using (FlightLogReader reader = FlightLogReader.Open(logPath))
                while (reader.Next())
                {
                    if (reader.Kind == LogFormat.EventBlock)
                    {
                        reader.ReadEvent(out LoggedEvent e);
                        EventKind k = e.Event.Kind;
                        if (e.Event.Object >= 0 && (k == EventKind.ContactMade || k == EventKind.PropStrike || k == EventKind.BladeStrike || k == EventKind.Impact || k == EventKind.Crash)
                            && touches++ == 0)
                            first = k + " on " + world.ObjectName(e.Event.Object) + " at t = " + (e.Step * FlightCore.Dt).ToString("0.000") + " s";
                        continue;
                    }
                    if (reader.Kind != LogFormat.StepBlock) continue;
                    reader.ReadStep(out LoggedStep step);
                    var at = new Capsule(step.State.Position, step.State.Position, 0);
                    int n = world.StaticContacts(at, at, Near, found, QueryFilter.NoGround);
                    if (n > found.Length) Assert.Fail(n + " shapes within " + Near + " m, more than the buffer holds");
                    for (int i = 0; i < n; i++)
                        if (found[i].Distance < closest)
                        {
                            closest = found[i].Distance;
                            by = world.ObjectName(found[i].Object) + " " + found[i].Object + "/" + found[i].Shape + " at t = " + (step.State.Step * FlightCore.Dt).ToString("0.000") + " s";
                        }
                }
            return (touches, first, closest, by);
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
