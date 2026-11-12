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
using Fpv450.Sim.Tether;
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
        /// <summary>physics_materials.json's hash at the physics integration 2 with p7f-damage (model 40): both sides' fields on
        /// every row (p7f-damage 1.3's integration note).</summary>
        const ulong MergedMaterialsHash = 0x2932AC6166D91022UL;

        // The player's copies alone (AppData.PlayerFiles under a folder of their own, as Fpv450.Build copies them) give the
        // drone, the farmland's map package with its objects (w3-farmland-ground 3.1 and 1.2: placements.json, the vendor lists
        // and each variant's metadata), the test range's, back for House (w10-play-maps 1.1), and surfaces.json and
        // tether.json byte for byte the repository's, so a pilot's log carries the hashes tools/replay checks against the
        // repository's files by default.
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
                FlightWorldChoice farmland = FlightWorlds.Farmland(copies);
                Assert.That(farmland.Id, Is.EqualTo(FlightWorlds.FarmlandMap));
                Assert.That(farmland.Open().ContentHash, Is.EqualTo(FlightWorlds.Farmland(AppData.Root).Open().ContentHash), "the same map as the repository's");
                FlightWorldChoice range = FlightWorlds.TestRange(copies);
                Assert.That(range.Id, Is.EqualTo(FlightWorlds.TestRangeMap));
                Assert.That(range.Open().ContentHash, Is.EqualTo(FlightWorlds.TestRange(AppData.Root).Open().ContentHash), "House: the same test range as the repository's");
                Assert.That(System.Array.ConvertAll(range.StartPoints, s => s.Name), Is.EqualTo(new[] { FlightWorlds.HousesStart }), "House flies from the houses' start");
                foreach (string table in new[] { SurfaceTable.RelativePath, TetherTable.RelativePath, PhysicsMaterials.RelativePath })
                {
                    Assert.That(files, Does.Contain(table), "the player carries " + table);
                    Assert.That(File.ReadAllBytes(Path.Combine(copies, table)), Is.EqualTo(File.ReadAllBytes(AppData.File(table))), table + " byte for byte");
                }
                // D-079: the merged physics_materials.json, every row with what a blade meets (p7f-damage 1.1a), at its hash;
                // a row without the blade's fields is refused.
                string json = File.ReadAllText(Path.Combine(copies, PhysicsMaterials.RelativePath));
                MaterialRow[] rows = PhysicsMaterials.Parse(json, out ulong hash);
                TestContext.WriteLine("physics_materials.json: " + rows.Length + " rows, 0x" + hash.ToString("X16"));
                Assert.That(hash, Is.EqualTo(MergedMaterialsHash), "the physics integration 2's merged table");
                Assert.That(System.Array.TrueForAll(rows, r => r.Hardness > 0 && r.Density > 0), "every row's hardness and density");
                int at = json.IndexOf("\"hardness_pa\"", json.IndexOf("\"materials\"", System.StringComparison.Ordinal), System.StringComparison.Ordinal); // the first row's
                Assert.That(at, Is.GreaterThan(0));
                string stripped = json.Substring(0, at) + "\"hardness_pa_gone\"" + json.Substring(at + "\"hardness_pa\"".Length);
                var refused = Assert.Throws<System.FormatException>(() => PhysicsMaterials.Parse(stripped, out _));
                StringAssert.Contains("hardness_pa", refused.Message);
            }
            finally
            {
                if (Directory.Exists(copies)) Directory.Delete(copies, true);
            }
        }

        // p5-drone's bottle and stand as the flight draws them: the build carries both prefabs; the bottle sits at its seat,
        // drone.json's payload underside, scaled as the mass build scales the part (none at 0 kg); D02, drawn at foot 0 and
        // yaw 0, stands with every shape of the physics' stand where the flight put it.
        [Test]
        public void TheBottleAndTheStandAreDrawnAsThePhysicsHasThem()
        {
            DroneModels models = DroneModels.Load();
            Assert.That(models, Is.Not.Null);
            Assert.That(models.legsOn != null && models.legsOff != null, "ProjectSetup.Apply's D01 prefabs");
            Assert.That(models.bottle != null && models.bottle.name == "D01_bottle", "ProjectSetup.Apply's D01_bottle");
            Assert.That(models.stand != null && models.stand.name == "D02_launch_stand", "ProjectSetup.Apply's D02");

            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
            DronePart payload = spec.Parts[spec.Payload.Part];
            Assert.That(DroneModels.BottleSeat.y, Is.EqualTo(payload.Position.Y - payload.Diameter / 2).Within(1e-6), "the payload's underside");
            Assert.That((DroneModels.BottleSeat.x, DroneModels.BottleSeat.z), Is.EqualTo((0f, 0f)));
            Assert.That(DroneModels.BottleDrawnKg, Is.EqualTo(payload.Mass), "the bottle is drawn at the file's payload");
            Assert.That(DroneModels.BottleScale(0), Is.EqualTo(0f), "no payload, no bottle");
            Assert.That(DroneModels.BottleScale(1.0), Is.EqualTo(1f));
            Assert.That(DroneModels.BottleScale(0.1), Is.EqualTo(0.4642f).Within(1e-4));
            Assert.That(DroneModels.BottleScale(2.0), Is.EqualTo(1.2599f).Within(1e-4));
            foreach (double kg in new[] { 0.1, 0.7, 1.5, 2.0 })
            {
                MassModel m = spec.BuildMass(new Loadout { Payload = kg, Legs = true });
                Assert.That(m.Part(spec.Payload.Part).Diameter / payload.Diameter, Is.EqualTo(DroneModels.BottleScale(kg)).Within(1e-6), kg + " kg: as the mass build scales the part");
            }

            var drawn = LaunchStand.Build(spec, default, 0, 0);
            foreach (double yaw in new[] { 0, 0.4, System.Math.PI / 2, System.Math.PI, -2.1 })
            {
                var foot = new Double3(40, 1.25, 45);
                RuntimeObject placed = LaunchStand.Build(spec, foot, yaw, 0);
                DroneModels.StandPose(placed, out Vector3 at, out Quaternion turn);
                Assert.That(Vector3.Distance(at, new Vector3(40f, 1.25f, 45f)), Is.LessThan(1e-5f), "yaw " + yaw + ": the foot");
                for (int s = 0; s < placed.Shapes.Length; s++)
                {
                    Vector3 want = V(placed.Shapes[s].Center), got = at + turn * V(drawn.Shapes[s].Center);
                    Assert.That(Vector3.Distance(got, want), Is.LessThan(1e-5f), "yaw " + yaw + ": shape " + s);
                }
            }
        }

        static Vector3 V(in Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

        static ulong MaterialsHash()
        {
            PhysicsMaterials.Parse(File.ReadAllText(AppData.File(PhysicsMaterials.RelativePath)), out ulong hash);
            return hash;
        }

        /// <summary>The link's dips and break from the physics (p7d-tether 1.16), their causes by name; the stub's whole link
        /// before a flight.</summary>
        [Test]
        public void TheFeedReadsTheLinksCauses()
        {
            var feed = new FlightFeedSignals();
            feed.Update(default, false, 0, 0, 0);
            Assert.That(new object[] { feed.LinkState, feed.LinkDips, feed.LinkInDip, feed.LinkLossCause, feed.TetherBreakMechanism },
                Is.EqualTo(new object[] { Fpv450.Feed.LinkState.Intact, 0, false, "", "" }), "the stub's link before a flight");

            var dip = new SimSignals { Live = FeedSignalSet.LinkState | FeedSignalSet.LinkMargin, LinkDips = 2, LinkInDip = true, LinkLossCause = (byte)FiberMember.Twig };
            feed.Update(dip, true, 0, 0, 0);
            Assert.That(FeedSignalLog.Value(feed, FeedSignal.LinkState), Is.EqualTo("Intact, dip 2 on Twig"));

            var cut = new SimSignals
            {
                Live = FeedSignalSet.LinkState | FeedSignalSet.LinkMargin, LinkState = Fpv450.Sim.Flight.LinkState.Broken,
                LinkMarginDb = double.NegativeInfinity, LinkDips = 2, TetherBreakStep = 17466, TetherBreakMechanism = (byte)TetherMechanism.Prop,
            };
            feed.Update(cut, true, 0, 0, 0);
            Assert.That(feed.LinkState, Is.EqualTo(Fpv450.Feed.LinkState.Broken));
            Assert.That(FeedSignalLog.Value(feed, FeedSignal.LinkState), Is.EqualTo("Broken by Prop"));
        }

        // D-087: R, like every new flight, ends whatever loss the feed was playing, so the new flight's first field is the
        // picture: a recovering dropout on Godot's timer (D-085), one held by the fiber's dip, and the staged loss of a break
        // to the terminal blue. Two feeds on one seed reach the same lost field; the new flight (its link whole, no dips)
        // resets one. The link's reading alone ends the dip's and the break's, but the timer's dropout ran on into the new
        // flight, up to 0.8 s of its tell and outage: the pilot's "R doesn't restart RIGHT AWAY" after a signal loss.
        [Test]
        public void ANewFlightEndsTheFeedsLossAtOnce()
        {
            foreach (string loss in new[] { "the timer's dropout", "the dip's dropout", "the break's staged loss", "the terminal blue" })
            {
                FeedEvents reset = Lost(loss, out FeedLink resetLink), kept = Lost(loss, out FeedLink keptLink);
                Assert.That(reset.Loss.Current, Is.Not.EqualTo(FeedLoss.State.Picture), loss + ": the picture lost");
                FlightScene.NewFlightFeed(reset);
                var whole = new StubFeedSignals(); // the new flight's link: intact, no dips
                FeedLoss after = Field(reset, resetLink, whole), ran = Field(kept, keptLink, whole);
                TestContext.WriteLine(loss + ": " + after.Current + " after R; without the reset " + ran.Current + (ran.Busy ? ", still running" : ""));
                Assert.That((after.Current, after.Busy, after.Tell), Is.EqualTo((FeedLoss.State.Picture, false, 0f)), loss + ": the new flight's first field is the picture");
                if (loss == "the timer's dropout") Assert.That(ran.Current, Is.Not.EqualTo(FeedLoss.State.Picture), "without it the timer's dropout runs on");
            }
        }

        // A flight's feed on seed 7 to the first field its loss shows: the timer's dropout (the timer on, the link whole), the
        // dip's (a dip from field 50), or a break at field 50 (the staged loss's first lost field, or 100 fields on, blue).
        static FeedEvents Lost(string loss, out FeedLink link)
        {
            bool timer = loss == "the timer's dropout";
            var events = new FeedEvents(7) { DropoutTimer = timer };
            link = new FeedLink();
            var s = new StubFeedSignals();
            for (int field = 0; field < 5000; field++)
            {
                if (field == 50 && loss == "the dip's dropout")
                {
                    s.LinkDips = 1;
                    s.LinkInDip = true;
                }
                if (field == 50 && (loss == "the break's staged loss" || loss == "the terminal blue")) s.LinkState = Fpv450.Feed.LinkState.Broken;
                FeedLoss l = Field(events, link, s);
                bool lost = l.Current != FeedLoss.State.Picture;
                if (lost && (loss != "the terminal blue" || field >= 150)) return events;
            }
            Assert.Fail(loss + " never showed");
            return events;
        }

        // The rig's field: the link read, then the field stepped.
        static FeedLoss Field(FeedEvents events, FeedLink link, IFeedSignals s)
        {
            link.Apply(s, events.Loss, events.Field + 1);
            events.AdvanceTo(events.Field + 1, 0f);
            return events.Loss;
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
                Sticks = new StickSample { Throttle = 0.5, Arm = true },
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
            StringAssert.Contains(FlightOsd.SymThr + " 47", grid, "the throttle from the sticks the flight flew (D-080)");
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
            Assert.That(FlightScene.FeedKey, Is.Not.EqualTo(Key.V).And.Not.EqualTo(Key.C), "the review feed's key left V; C is the noise's (D-078)");

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
                    replay = FlightReplay.Run(f, id => id == "floor" ? FlightWorld.Floor() : null, spec.ContentHash, materialsHash: MaterialsHash());
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

        // D-078: the app's keys over the flight are none of the flight's (KeyboardFallback's): each held alone, the flight
        // reads no key. None is V, F or Esc, and no two are the same.
        [Test]
        public void TheAppsKeysNeverReachTheFlight()
        {
            Assert.That(FlightScene.RestartKey, Is.EqualTo(Key.R));
            Assert.That(FlightScene.NoiseKey, Is.EqualTo(Key.C));
            Assert.That(FlightScene.SticksKey, Is.EqualTo(Key.X));
            Key[] keys = { FlightScene.RestartKey, FlightScene.NoiseKey, FlightScene.SticksKey };
            Assert.That(keys, Is.Unique);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("D-078 test keyboard");
            keyboard.MakeCurrent();
            try
            {
                foreach (Key key in keys)
                {
                    Assert.That(key, Is.Not.EqualTo(FlightScene.ViewKey).And.Not.EqualTo(FlightScene.FeedKey).And.Not.EqualTo(Key.Escape));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    InputSystem.Update();
                    Assert.That(keyboard[key].isPressed, key + " held");
                    Assert.That(KeyboardFallback.ReadKeys(), Is.EqualTo(KeyboardKeys.None), key + " is not a flight key");
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        // D-078: C turns the feed's noise off and on for the rest of the app's run: the next flight's scene keeps it, and
        // the view stays as it was.
        [Test]
        public void CTogglesTheNoiseForTheRun()
        {
            bool before = FlightScene.FeedNoise;
            Assert.That(before, Is.True, "on at the app's start");
            FlightScene first = new GameObject("Flight").AddComponent<FlightScene>(), next = null;
            try
            {
                first.Pressed(false, false, true);
                Assert.That(FlightScene.FeedNoise, Is.False, "C: off");
                Assert.That(first.Chase, Is.False, "the view stays");
                next = new GameObject("Flight 2").AddComponent<FlightScene>();
                Assert.That(FlightScene.FeedNoise, Is.False, "the next flight's scene keeps it");
                next.Pressed(false, false, true);
                Assert.That(FlightScene.FeedNoise, Is.True, "C again: on");
            }
            finally
            {
                if (FlightScene.FeedNoise != before) first.Pressed(false, false, true);
                Object.DestroyImmediate(first.gameObject);
                if (next != null) Object.DestroyImmediate(next.gameObject);
            }
        }

        // D-078: X turns Betaflight's stick overlay on the OSD on and off for the rest of the app's run: the scene's OSD
        // draws it, the next flight's scene keeps it, and the noise and the view stay as they were.
        [Test]
        public void XTogglesTheOsdsSticksForTheRun()
        {
            bool before = FlightScene.StickOverlay, noise = FlightScene.FeedNoise;
            Assert.That(before, Is.False, "off at the app's start");
            FlightScene first = new GameObject("Flight").AddComponent<FlightScene>(), next = null;
            try
            {
                first.Pressed(false, false, false, true);
                Assert.That(FlightScene.StickOverlay && first.Osd.StickOverlay, "X: on, in the OSD");
                Assert.That((FlightScene.FeedNoise, first.Chase), Is.EqualTo((noise, false)), "the noise and the view stay");
                next = new GameObject("Flight 2").AddComponent<FlightScene>();
                next.Pressed(false, false);
                Assert.That(next.Osd.StickOverlay, Is.True, "the next flight's scene keeps it");
                next.Pressed(false, false, false, true);
                Assert.That(FlightScene.StickOverlay || next.Osd.StickOverlay, Is.False, "X again: off");
            }
            finally
            {
                if (FlightScene.StickOverlay != before) first.Pressed(false, false, false, true);
                Object.DestroyImmediate(first.gameObject);
                if (next != null) Object.DestroyImmediate(next.gameObject);
            }
        }

        // An exact keyboard flight on the floor (Threaded false: Tick runs the steps due on the test's clock), its keys held
        // on the test keyboard and read a 1 ms frame at a time as the app reads them: the scene's keys, then the flight's
        // (FlightSessionHost ticks the session with KeyboardFallback.ReadKeys). Enter arms, W climbs, D yaws, the up arrow
        // pitches, Enter disarms, and End flight ends it 100 ms later (a disarm ends nothing, D-062). With vEveryMs, V is
        // held 50 ms in every vEveryMs, and each press must flip the view. A
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
                for (int ms = 0; ms < 1700 && session.State != SessionState.Ended; ms++)
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
                Assert.That((session.Core.State.Armed, session.State), Is.EqualTo((false, SessionState.Flying)), "disarmed by Enter, still flying");
                session.EndFlight();
                Assert.That(session.End.Kind, Is.EqualTo(FlightEndKind.Pilot), "End flight");
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
