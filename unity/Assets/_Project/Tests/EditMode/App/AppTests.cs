using System.Collections.Generic;
using System.IO;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.Feed;
using Fpv450.Input;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SimSignals = Fpv450.Sim.Flight.FeedSignals;

namespace Fpv450.Tests.App
{
    // The app's screens and player (p6-input-app 1.4b): what of them runs without a scene.
    public sealed class AppTests
    {
        // The player's copies alone (AppData.PlayerFiles under a folder of their own, as Fpv450.Build copies them) give the
        // drone and the test range's map.
        [Test]
        public void ThePlayerCarriesEveryFileTheFlightReads()
        {
            string copies = Path.Combine(Path.GetTempPath(), "fpv450-player-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                List<string> files = AppData.PlayerFiles(AppData.Root);
                foreach (string file in files)
                {
                    string to = Path.Combine(copies, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    File.Copy(AppData.File(file), to);
                }
                TestContext.WriteLine(files.Count + " files: " + string.Join(", ", files));
                Assert.That(DroneSpec.Parse(File.ReadAllText(Path.Combine(copies, AppData.DroneFile))), Is.Not.Null);
                FlightWorldChoice range = FlightWorlds.TestRange(copies);
                Assert.That(range.Id, Is.EqualTo("test-range"));
                Assert.That(range.Open().ContentHash, Is.EqualTo(FlightWorlds.TestRange(AppData.Root).Open().ContentHash), "the same map as the repository's");
            }
            finally
            {
                if (Directory.Exists(copies)) Directory.Delete(copies, true);
            }
        }

        [Test]
        public void TheFeedReadsThePhysicsWhereItIsLive()
        {
            var feed = new FlightFeedSignals();
            feed.Osd.Cells = 6;
            feed.Update(default, false, 0, 0, 0); // before a flight: the stub's pack, disarmed
            feed.BuildOsd(0);
            Assert.That(feed.SourceOf(FeedSignal.OsdState), Is.EqualTo(SignalSource.Stub));
            Assert.That(Grid(feed), Does.StartWith("DISARMED "));
            StringAssert.Contains("$25.2в", Grid(feed), "the stub's pack");

            feed.Osd.Reset();
            var first = new SimSignals { Live = FeedSignalSet.PackVolts | FeedSignalSet.MotorCurrent, Step = FlightCore.FieldSteps - 1 };
            feed.Update(first, false, 0, 0, 0); // the physics' means before its first whole field are 0 V: not sampled
            feed.BuildOsd(5);
            Assert.That(Grid(feed), Does.Not.Contain("в"), "no voltage before the first whole field");

            var s = new SimSignals
            {
                Live = FeedSignalSet.PackVolts | FeedSignalSet.MotorCurrent | FeedSignalSet.CameraAngularVelocity,
                Step = 2 * FlightCore.FieldSteps,
                PackVolts = 23.5,
                MotorCurrentTotalA = 41.2,
                MotorCurrentA = new Corners { RearRight = 10.1, FrontRight = 10.2, RearLeft = 10.3, FrontLeft = 10.6 },
                CameraAngularVelocity = new Double3(0.1, -0.2, 0.3),
                LinkMarginDb = 3, // not live: the stub's
            };
            feed.Update(s, true, 12.4, 250, 75.3);
            Assert.That(feed.SourceOf(FeedSignal.PackVolts), Is.EqualTo(SignalSource.Physics));
            Assert.That(feed.PackVolts, Is.EqualTo(23.5f));
            Assert.That(feed.MotorCurrentTotalA, Is.EqualTo(41.2f));
            Assert.That(feed.MotorCurrentA(3), Is.EqualTo(10.6f));
            Assert.That(feed.CameraAngularVelocity.Y, Is.EqualTo(-0.2f));
            Assert.That(feed.SourceOf(FeedSignal.LinkMargin), Is.EqualTo(SignalSource.Stub));
            Assert.That(feed.LinkMarginDb, Is.EqualTo(float.PositiveInfinity), "the stub's link, until 7d");
            Assert.That(feed.SourceOf(FeedSignal.OsdState), Is.EqualTo(SignalSource.Physics), "the OSD's content model, from the pack's signals (p4-feed 1.5)");

            feed.BuildOsd(9);
            string grid = Grid(feed);
            Assert.That(grid, Does.StartWith("AIR "), "armed");
            StringAssert.Contains("SVINORIZ 10 OPTO", grid);
            StringAssert.Contains("! SAFE !", grid);
            StringAssert.Contains(">12m", grid, "the altitude from the flight");
            StringAssert.Contains("01:15", grid, "the timer from the flight");
            StringAssert.Contains("0.25", grid, "the distance home, km");
            StringAssert.Contains("23.5", grid, "the pack from the physics");
            StringAssert.Contains("41.20", grid, "the current from the physics");
        }

        static string Grid(FlightFeedSignals feed)
        {
            string grid = "";
            for (int r = 0; r < FeedOsd.Rows; r++)
            {
                for (int c = 0; c < FeedOsd.Cols; c++) grid += feed.OsdCell(r, c);
                grid += "\n";
            }
            return grid;
        }

        [Test]
        public void ASlowRadioIsNamedAfterFinding13()
        {
            Assert.That(CalibrateScreen.SlowRadioUs, Is.EqualTo(4375), "EdgeTX's floor gain on the jump rule's fastest accepted sweep");
            Assert.That(CalibrateScreen.SlowRadioNote(2000), Is.Null, "a TX12 in joystick mode, every 2 ms");
            Assert.That(CalibrateScreen.SlowRadioNote(4000), Is.Null);
            Assert.That(CalibrateScreen.SlowRadioNote(6667), Is.EqualTo("The radio updates its USB output only every 6.7 ms (an RF module is on): switch it off for the sim."));
            StringAssert.Contains("every 20 ms", CalibrateScreen.SlowRadioNote(20000));
        }

        [Test]
        public void TheSetupClockReadsHoursAndMinutes()
        {
            Assert.That(SetupScreen.Clock(0), Is.EqualTo("00:00"));
            Assert.That(SetupScreen.Clock(17.5), Is.EqualTo("17:30"));
            Assert.That(SetupScreen.Clock(23 + 59 / 60.0), Is.EqualTo("23:59"));
        }

        // D-052: the build's preset, the saved one, or -quality's, which is saved; no screen offers it.
        [Test]
        public void TheQualityPresetIsTheBuildsTheSavedOneOrTheCommandLines()
        {
            string[] names = { "Low", "Medium", "High" };
            string[] none = { "450fpv.exe", "-windowed" };
            Assert.That(QualityPreset.Pick(none, names, null, out bool given, out string problem), Is.EqualTo(-1), "none saved: the build's");
            Assert.That((given, problem), Is.EqualTo((false, (string)null)));
            Assert.That(QualityPreset.Pick(none, names, "Medium", out given, out _), Is.EqualTo(1), "the saved one");
            Assert.That(given, Is.False);
            Assert.That(QualityPreset.Pick(new[] { "450fpv.exe", "-quality", "low" }, names, "Medium", out given, out problem), Is.EqualTo(0), "-quality's, in any case");
            Assert.That((given, problem), Is.EqualTo((true, (string)null)));
            Assert.That(QualityPreset.Pick(new[] { "450fpv.exe", "-quality", "Ultra" }, names, "Medium", out given, out problem), Is.EqualTo(1), "an unknown name: the saved one");
            Assert.That((given, problem), Is.EqualTo((false, "-quality 'Ultra' is not one of Low, Medium, High: it is ignored.")));
            Assert.That(QualityPreset.Pick(new[] { "450fpv.exe", "-quality" }, names, null, out given, out problem), Is.EqualTo(-1));
            Assert.That(problem, Is.Not.Null, "-quality without a name");

            string dir = Path.Combine(Path.GetTempPath(), "fpv450-quality-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                Assert.That(QualityPreset.Load(dir, out string error), Is.Null);
                Assert.That(error, Is.Null, "no file: nothing saved");
                QualityPreset.Save(dir, "Low");
                QualityPreset.Save(dir, "Medium");
                Assert.That(QualityPreset.Load(dir, out error), Is.EqualTo("Medium"), "the last saved");
                Assert.That(error, Is.Null);
                File.WriteAllText(QualityPreset.PathIn(dir), "{ \"preset\": ");
                Assert.That(QualityPreset.Load(dir, out error), Is.Null);
                StringAssert.StartsWith("quality.json could not be read", error);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        // QA's finding 16: the keyboard arms with Enter, a toggle; only the radio's switch is held off until seen off.
        [Test]
        public void TheArmingHintIsTheInputs()
        {
            Assert.That(FlightScene.ArmHint(InputSource.Keyboard), Is.EqualTo("Arm: Enter, with the throttle down (S)"));
            Assert.That(FlightScene.ArmHint(InputSource.Radio), Is.EqualTo("Arm: switch off, then on, with the throttle down"));
        }

        // D-055: V switches the view between the FPV camera at the lens and the third-person chase view, in every build, and
        // nothing else. The camera: at the mount, then 2.2 m behind and 0.7 m above the drone on its heading, 14° down. The
        // flight: two exact keyboard flights (the session's steps on the test's clock, a frame a ms, the same seed) fly the
        // same keys through the Input System, the second with V held 50 ms in every 200. Each press flips the scene's view,
        // while the flight's keys (KeyboardFallback's), and so the logs, stay the same byte for byte; the flight with V
        // replays bit for bit.
        [Test]
        public void VSwitchesOnlyTheView()
        {
            Assert.That(FlightScene.ViewKey, Is.EqualTo(Key.V));
            Assert.That(FlightScene.FeedKey, Is.Not.EqualTo(Key.V).And.Not.EqualTo(Key.C), "the review feed's key left V; C is retired");

            var body = new GameObject("Drone").transform;
            var eye = new GameObject("FpvCamera").transform;
            eye.SetParent(body, false);
            FlightScene scene = new GameObject("Flight").AddComponent<FlightScene>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("D-055 test keyboard");
            keyboard.MakeCurrent();
            string dir = Path.Combine(Path.GetTempPath(), "fpv450-view-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                body.SetPositionAndRotation(new Vector3(40f, 0.25f, 45f), Quaternion.Euler(0f, 180f, 0f));
                var mount = new Vector3(0f, 0.015f, 0.115f);
                FlightScene.PlaceCamera(eye, body, mount, false);
                Assert.That(Vector3.Distance(eye.localPosition, mount), Is.LessThan(1e-6f), "the FPV camera at the lens");
                Assert.That(Quaternion.Angle(eye.localRotation, Quaternion.identity), Is.LessThan(1e-3f), "looking along the drone's +z");
                FlightScene.PlaceCamera(eye, body, mount, true);
                Assert.That(Vector3.Distance(eye.position, new Vector3(40f, 0.95f, 47.2f)), Is.LessThan(1e-4f), "2.2 m behind the drone heading 180°, 0.7 m above");
                Assert.That(Vector3.Angle(eye.forward, Quaternion.Euler(14f, 180f, 0f) * Vector3.forward), Is.LessThan(0.01f), "along its heading, 14° down");
                FlightScene.PlaceCamera(eye, body, mount, false);
                Assert.That(Vector3.Distance(eye.localPosition, mount), Is.LessThan(1e-6f), "and back at the lens");

                DroneSpec spec = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
                byte[] plain = Fly(spec, keyboard, scene, Path.Combine(dir, "plain"), 0, out int noPresses, out _);
                Assert.That(noPresses, Is.EqualTo(0));
                byte[] withV = Fly(spec, keyboard, scene, Path.Combine(dir, "v"), 200, out int presses, out string log);
                TestContext.WriteLine("V pressed " + presses + " times; the logs: " + plain.Length + " and " + withV.Length + " bytes");
                Assert.That(withV, Is.EqualTo(plain), "the flight's log, byte for byte, with V or without");

                ReplayResult replay;
                using (FileStream f = File.OpenRead(log))
                    replay = FlightReplay.Run(f, id => id == "floor" ? FlightWorld.Floor() : null, spec.ContentHash);
                TestContext.WriteLine("replay: " + replay.Outcome + ", " + replay.Steps + " steps " + replay.Message);
                Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
                Assert.That(replay.Steps, Is.GreaterThan(1500));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                Object.DestroyImmediate(scene.gameObject);
                Object.DestroyImmediate(body.gameObject);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        // An exact keyboard flight on the floor (Threaded false: Tick runs the steps due on the test's clock), its keys held
        // on the test keyboard and read a 1 ms frame at a time as the app reads them: the scene's keys, then the flight's
        // (FlightSessionHost ticks the session with KeyboardFallback.ReadKeys). Enter arms, W climbs, D yaws, the up arrow
        // pitches, Enter disarms. With vEveryMs, V is held 50 ms in every vEveryMs, and each press must flip the view. A
        // manual update in edit mode gives keys' states but not wasPressedThisFrame (the Input System restores the player's
        // update count after an editor update), so the press is the V key's state turning on; the players' smoke test
        // presses V through FlightScene's own read. Returns the log's bytes.
        static byte[] Fly(DroneSpec spec, Keyboard keyboard, FlightScene scene, string folder, int vEveryMs, out int presses, out string log)
        {
            MaterialRow[] materials = PhysicsMaterials.Parse(File.ReadAllText(AppData.File(PhysicsMaterials.RelativePath)), out ulong hash);
            var clock = new TestClock { Now = 1000000 };
            var reader = new RadioReader(() => null);
            var session = new FlightSession(new SessionInputs
            {
                Drone = spec,
                Materials = materials,
                MaterialsHash = hash,
                LogFolder = folder,
                Build = "apptests",
                Reader = reader,
                Clock = clock,
                Threaded = false,
                KeyboardAllowed = true,
                NewSeed = () => 0xD055,
            });
            presses = 0;
            int edges = 0;
            bool lastV = false;
            var held = new List<Key>();
            try
            {
                for (int ms = 0; ms < 2000 && session.State != SessionState.Ended; ms++)
                {
                    if (ms == 10) session.Start();
                    held.Clear();
                    if (ms == 40 || ms == 1600) held.Add(Key.Enter);
                    if (ms >= 100 && ms < 1300) held.Add(Key.W);
                    if (ms >= 1300 && ms < 1400) held.Add(Key.D);
                    if (ms >= 1400 && ms < 1500) held.Add(Key.UpArrow);
                    if (vEveryMs > 0 && ms % vEveryMs < 50) held.Add(Key.V);
                    if (vEveryMs > 0 && ms % vEveryMs == 0) edges++;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(held.ToArray()));
                    InputSystem.Update();
                    clock.Now += FlightCore.StepUs;
                    reader.Pump(0);
                    bool chase = scene.Chase, v = keyboard[FlightScene.ViewKey].isPressed;
                    scene.Pressed(v && !lastV, false);
                    lastV = v;
                    if (scene.Chase != chase) presses++;
                    session.Tick(KeyboardFallback.ReadKeys());
                }
                Assert.That(session.Input, Is.EqualTo(InputSource.Keyboard));
                Assert.That(session.End.Kind, Is.EqualTo(FlightEndKind.Disarm), "disarmed by Enter");
                Assert.That(presses, Is.EqualTo(edges), "each V press flips the view, and only V's");
                log = session.LogPath;
            }
            finally
            {
                session.Dispose();
                reader.Dispose();
            }
            return File.ReadAllBytes(log);
        }

        sealed class TestClock : IHostClock
        {
            public long Now;
            public long NowUs() => Now;
        }
    }
}
