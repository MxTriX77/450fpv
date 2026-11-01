using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Fpv450.App.Session;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;
using StartPoint = Fpv450.App.Session.StartPoint;

namespace Fpv450.Tests.Session
{
    // app-flow (p6-input-app 1.4a, design 6 and 7, APP-03…05): the flight session's logic with no scene, on the
    // repository's drone over the world-query stub's floor. A test radio of the TX12's layout reports every 2 ms on a
    // clock the test sets; the input thread's passes (RadioReader.Pump) and the session's frames (Tick) run once a ms, and
    // the session runs the steps due (Threaded false), so every flight is exact. The last test runs the flight thread
    // in real time.
    public sealed class SessionTests
    {
        static DroneSpec _spec;
        static MaterialRow[] _materials;
        static ulong _materialsHash;
        string _dir;

        [OneTimeSetUp]
        public void LoadDrone()
        {
            _spec = DroneSpec.Parse(File.ReadAllText(RepositoryFile("assets-src/drone/drone.json")));
            _materials = PhysicsMaterials.Parse(File.ReadAllText(RepositoryFile(PhysicsMaterials.RelativePath)), out _materialsHash);
        }

        [SetUp]
        public void MakeFolder()
        {
            _dir = Path.Combine(Path.GetTempPath(), "fpv450-session-" + Guid.NewGuid().ToString("N"));
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

        // ---- The test range's starts (1.4b) -------------------------------------------------------------------------------

        [Test]
        public void TheTestRangeListsItsStartsAndFliesOnItsMap()
        {
            string root = RepositoryRoot();
            Assert.That(Path.GetFullPath(FlightWorlds.PlacementsPath(root)), Is.EqualTo(Path.GetFullPath(RepositoryFile("maps/test-range/placements.json"))));
            FlightWorldChoice world = FlightWorlds.TestRange(root);
            Assert.That(world.Id, Is.EqualTo("test-range"), "MapWorld's map id, which the log records so replay reopens it");
            Assert.That(world.Open().Query, Is.InstanceOf<MapWorld>());
            Assert.That(world.Open().ContentHash, Is.EqualTo(MapWorld.Load(root, "test-range").ContentHash));
            Assert.That(world.StartPoints.Length, Is.EqualTo(2));
            StartPoint track = world.StartPoints[0], field = world.StartPoints[1];
            Assert.That((track.Name, track.X, track.Z, track.Stand), Is.EqualTo(("track", -178.0, -40.0, false)));
            Assert.That((field.Name, field.X, field.Z, field.Stand), Is.EqualTo(("field", 40.0, 45.0, true)));
            Assert.That(track.Yaw, Is.EqualTo(Math.PI / 2).Within(1e-15), "heading 90°: east");
            Assert.That(field.Yaw, Is.EqualTo(Math.PI).Within(1e-15), "heading 180°: south");

            foreach (StartPoint at in world.StartPoints)
            {
                SessionInputs inputs = Inputs();
                world.ApplyTo(inputs);
                inputs.Choices = new FlightChoices { StartPoint = at.Name, Legs = !at.Stand };
                FlightSetup setup = SessionSetup.Build(inputs, 1, out Loadout loadout, out StartPoint start);
                Assert.That(start.Name, Is.EqualTo(at.Name));
                Assert.That(setup.StartPosition.X, Is.EqualTo(at.X).Within(1e-12));
                Assert.That(setup.StartPosition.Z, Is.EqualTo(at.Z).Within(1e-12));
                RuntimeObject[] objects = setup.RuntimeObjects ?? new RuntimeObject[0];
                if (!at.Stand)
                {
                    Assert.That(objects, Is.Empty, at.Name + ": legs on, no stand");
                    continue;
                }
                Assert.That(objects.Length, Is.EqualTo(1));
                Assert.That(objects[0].Name, Is.EqualTo(LaunchStand.Name));
                double x = 0, z = 0;
                for (int c = 0; c < 4; c++)
                {
                    x += objects[0].Shapes[c].Center.X / 4;
                    z += objects[0].Shapes[c].Center.Z / 4;
                }
                Assert.That(x, Is.EqualTo(at.X).Within(1e-9), "the stand's posts about the start");
                Assert.That(z, Is.EqualTo(at.Z).Within(1e-9));
            }
        }

        // ---- Setup to FlightSetup ---------------------------------------------------------------------------------------

        [Test]
        public void TheSetupBecomesTheFlight()
        {
            foreach (bool legs in new[] { true, false })
            {
                Rig rig = Make(i => i.Choices = new FlightChoices { Payload = 1.5, Legs = legs, Wind = WindLevel.Windy, TimeOfDay = 21.5, Rain = Rain.SevereRain });
                rig.Session.Start();
                FlightSetup s = rig.Session.Setup;
                Loadout l = rig.Session.Loadout;
                Assert.That((l.Payload, l.Legs, l.CoilPaidOut), Is.EqualTo((1.5, legs, 0.0)), "the loadout");
                Assert.That(s.Wind.Level, Is.EqualTo(WindLevel.Windy));
                Assert.That(s.Mass.Properties.Mass, Is.EqualTo(_spec.BuildMass(l).Properties.Mass));
                Assert.That(s.Settings.roll_srate, Is.EqualTo(FcSettings.Default.roll_srate));
                Assert.That(rig.Session.Choices.TimeOfDay, Is.EqualTo(21.5), "the scene's time of day");
                Assert.That(rig.Session.Choices.Rain, Is.EqualTo(Rain.SevereRain), "the scene's rain");
                if (legs)
                {
                    Assert.That(s.RuntimeObjects, Is.Empty);
                    Assert.That(s.StartPosition.Y, Is.EqualTo(SessionSetup.Standing(s.Contacts)), "standing on the skids");
                }
                else
                {
                    Assert.That(s.RuntimeObjects.Length, Is.EqualTo(1));
                    Assert.That(s.RuntimeObjects[0].Name, Is.EqualTo(LaunchStand.Name));
                    Assert.That(s.StartPosition, Is.EqualTo(LaunchStand.RestingOrigin(_spec, s.Contacts, default)), "resting on the stand's posts");
                }

                // Disarmed for 1.5 s: it rests where it was put, on its skids or on the stand.
                rig.Run(1500);
                FlightCore core = rig.Session.Core;
                double sink = s.StartPosition.Y - core.State.Position.Y;
                TestContext.WriteLine("legs " + (legs ? "on" : "off") + ": start y " + s.StartPosition.Y.ToString("0.0000") + " m, after 1.5 s "
                    + core.State.Position.Y.ToString("0.0000") + " m, " + core.Trace.ContactsTouching + " touching pairs");
                Assert.That(rig.Session.State, Is.EqualTo(SessionState.Flying));
                Assert.That(core.State.Crashed, Is.False);
                Assert.That(core.Trace.ContactsTouching, Is.GreaterThan(0));
                Assert.That(sink, Is.InRange(0, 0.01), "settled within 10 mm");

                rig.Session.EndFlight();
                Assert.That(rig.Session.End.Kind, Is.EqualTo(FlightEndKind.Pilot));
                PostFlight p = rig.Session.ReadSummary();
                LogHeader h = p.Log.Header;
                Assert.That(p.Log.Complete, p.Log.CutReason);
                Assert.That((h.Loadout.Payload, h.Loadout.Legs), Is.EqualTo((1.5, legs)));
                Assert.That(h.Wind.Level, Is.EqualTo(WindLevel.Windy));
                Assert.That(h.RuntimeObjects.Length, Is.EqualTo(legs ? 0 : 1));
                Assert.That(h.StartHostUs, Is.EqualTo(rig.Session.StartUs), "header.StartHostUs is the flight's step 0");
                Assert.That((h.Input, h.InputDevice), Is.EqualTo((InputSource.Radio, TestRadio.DeviceName)));
                Assert.That(h.InputIntervals.Count, Is.GreaterThan(0), "the report intervals before the flight");
                Assert.That(p.End.Kind, Is.EqualTo(FlightEndKind.Pilot));
            }
        }

        [Test]
        public void TheChoicesAreChecked()
        {
            Assert.That(new FlightChoices { Payload = 2.0 }.Check(), Is.Null);
            Assert.That(new FlightChoices { Payload = 0.3 }.Check(), Is.Null);
            Assert.That(new FlightChoices { Payload = 2.1 }.Check(), Is.Not.Null);
            Assert.That(new FlightChoices { Payload = 0.15 }.Check(), Is.Not.Null);
            Assert.That(new FlightChoices { TimeOfDay = 24 }.Check(), Is.Not.Null);
            Assert.That(new FlightChoices { Wind = WindLevel.Still }.Check(), Is.Not.Null);
            Rig rig = Make(i => i.Choices = new FlightChoices { Payload = 2.5 });
            Assert.Throws<ArgumentException>(() => rig.Session.Start());
        }

        // ---- Arm and disarm ---------------------------------------------------------------------------------------------

        [Test]
        public void ArmAndDisarmByTheControllersRules()
        {
            Rig rig = Make();
            TestRadio radio = rig.Radio;
            radio.Arm = true; // left on from before the flight
            rig.Session.Start();
            FlightCore core = rig.Session.Core;
            rig.Run(300);
            Assert.That(core.State.Armed, Is.False, "a switch left on does not arm: it must be seen off first");

            radio.Arm = false;
            rig.Run(50);
            radio.Throttle = 0.5;
            radio.Arm = true;
            rig.Run(100);
            Assert.That(core.State.Armed, Is.False, "on with the throttle up: refused");
            radio.Throttle = 0;
            rig.Run(100);
            Assert.That(core.State.Armed, Is.False, "the refusal holds until the switch is off");
            radio.Arm = false;
            rig.Run(50);
            radio.Arm = true;
            rig.Run(100);
            Assert.That(core.State.Armed, Is.True, "off, then on with the throttle down: armed");

            radio.Throttle = 0.3;
            rig.Run(500);
            radio.Throttle = 0;
            rig.Run(200);
            Assert.That(rig.Session.State, Is.EqualTo(SessionState.Flying));
            radio.Arm = false;
            long offAt = rig.Clock.Now;
            rig.Run(100);
            Assert.That(rig.Session.State, Is.EqualTo(SessionState.Ended));
            Assert.That(rig.Session.End.Kind, Is.EqualTo(FlightEndKind.Disarm));
            long endHostUs = rig.Session.StartUs + (rig.Session.End.Step - 1) * FlightCore.StepUs;
            TestContext.WriteLine("switch off at host " + offAt + " us; the flight ended at step " + rig.Session.End.Step + " (host " + endHostUs + " us, the RC frame's)");
            Assert.That(endHostUs - offAt, Is.InRange(0, 5 * FlightCore.StepUs), "the disarm at the next RC frame");

            PostFlight p = rig.Session.ReadSummary();
            Assert.That((p.End.Kind, p.End.Step), Is.EqualTo((rig.Session.End.Kind, rig.Session.End.Step)), "the summary's end is the session's");
            Assert.That(p.Log.Last.State.Step, Is.EqualTo(rig.Session.End.Step), "the log stops at the end's step");
            Assert.That(p.Cause(), Is.EqualTo("disarmed"));
        }

        // ---- The failsafe -----------------------------------------------------------------------------------------------

        [Test]
        public void TheFailsafeEndsAnArmedFlight()
        {
            Rig rig = Make();
            rig.Session.Start();
            rig.ArmCycle();
            rig.Radio.Throttle = 0.3;
            rig.Run(200);
            rig.Radio.Lost = true;
            long lostAt = rig.Clock.Now + FlightCore.StepUs; // the next pass sees it
            rig.Run(20);
            Assert.That(rig.Session.State, Is.EqualTo(SessionState.Ended));
            Assert.That(rig.Session.End.Kind, Is.EqualTo(FlightEndKind.Failsafe));
            // The failsafe sample is in force from the first step at or after the loss; the controller reads the sticks,
            // its arm switch among them, at its 250 Hz RC frame, so it disarms at the next frame: within 4 steps.
            long endHostUs = rig.Session.StartUs + (rig.Session.End.Step - 1) * FlightCore.StepUs;
            Assert.That(endHostUs - lostAt, Is.InRange(0, FlightController.LoopsPerRcFrame / FlightController.LoopsPerStep * FlightCore.StepUs - 1),
                "disarmed at the RC frame after the loss");

            PostFlight p = rig.Session.ReadSummary();
            TestContext.WriteLine("lost at host " + lostAt + " us; disarmed at the step at host " + endHostUs + " us; " + string.Join(" | ", p.Lines()));
            Assert.That((p.End.Kind, p.End.Step), Is.EqualTo((FlightEndKind.Failsafe, rig.Session.End.Step)));
            Assert.That(p.Log.Failsafes.Count, Is.EqualTo(1));
            Assert.That(p.Log.Failsafes[0].Stick.HostUs, Is.EqualTo(lostAt));
            StringAssert.StartsWith("failsafe", p.Cause());
        }

        [Test]
        public void AfterAFailsafeTheSwitchMustCycle()
        {
            Rig rig = Make();
            rig.Session.Start();
            rig.Run(100);
            rig.Radio.Lost = true;
            rig.Run(10);
            Assert.That(rig.Reader.Connected, Is.False);
            var back = new TestRadio(rig.Clock) { Arm = true }; // plugged back in with the switch on
            rig.Plugged = back;
            rig.Run(300);
            FlightCore core = rig.Session.Core;
            Assert.That(rig.Reader.Connected, Is.True);
            Assert.That(core.State.Armed, Is.False, "the switch on through the loss does not arm");
            Assert.That(rig.Reader.ArmHeld, Is.True);
            back.Arm = false;
            rig.Run(50);
            Assert.That(rig.Reader.ArmHeld, Is.False);
            back.Arm = true;
            rig.Run(50);
            Assert.That(core.State.Armed, Is.True, "cycled: armed");
            Assert.That(rig.Session.State, Is.EqualTo(SessionState.Flying), "a failsafe before arming ends nothing");
        }

        // QA's probes E3a and E3b (interim review, finding 1): armed at throttle 0.3, paused, the cable pulled during the
        // pause; E3b plugs it back in with the switch on 200 ms later, before Resume. Both are the failsafe: in force for
        // the first RC frame after Resume, logged once with the loss's stamp, and the summary says so. E3f (finding 8):
        // E3b, paused again 1, 2 or 3 ms after Resume, before that RC frame has run, and resumed 50 ms later.
        [TestCase(false, 0, TestName = "E3a: a radio lost during a pause is the failsafe")]
        [TestCase(true, 0, TestName = "E3b: a radio lost during a pause and plugged back in before Resume is the failsafe")]
        [TestCase(true, 1, TestName = "E3f: E3b paused again 1 ms after Resume is the failsafe")]
        [TestCase(true, 2, TestName = "E3f: E3b paused again 2 ms after Resume is the failsafe")]
        [TestCase(true, 3, TestName = "E3f: E3b paused again 3 ms after Resume is the failsafe")]
        public void ARadioLostDuringAPauseIsTheFailsafe(bool pluggedBackIn, int pausedAgainAfterMs)
        {
            Rig rig = Make(i =>
            {
                ulong seed = 0x0A31;
                i.NewSeed = () => seed++;
                i.Choices = new FlightChoices { Payload = 1.0, Legs = true };
            });
            rig.Session.Start();
            rig.ArmCycle();
            rig.Radio.Throttle = 0.3;
            rig.Run(300);
            rig.Session.Pause();
            long k = rig.Session.Core.State.Step;
            rig.Run(100);
            rig.Radio.Lost = true;
            long lostAt = rig.Clock.Now + FlightCore.StepUs; // the next pass sees it
            rig.Run(100);
            if (pluggedBackIn)
            {
                var back = new TestRadio(rig.Clock) { Arm = true, Throttle = 0.3 };
                rig.Radio = back;
                rig.Plugged = back;
                rig.Run(200);
                Assert.That(rig.Reader.Connected, Is.True, "back before Resume");
            }
            Assert.That(rig.Session.State, Is.EqualTo(SessionState.Paused));
            rig.Session.Resume();
            if (pausedAgainAfterMs > 0)
            {
                rig.Run(pausedAgainAfterMs);
                Assert.That(rig.Session.State, Is.EqualTo(SessionState.Flying), "the RC frame has not run yet");
                rig.Session.Pause();
                rig.Run(50);
                rig.Session.Resume();
            }
            rig.Run(100);

            Assert.That(rig.Session.State, Is.EqualTo(SessionState.Ended));
            Assert.That(rig.Session.End.Kind, Is.EqualTo(FlightEndKind.Failsafe));
            long rcFrame = FlightController.LoopsPerRcFrame / FlightController.LoopsPerStep;
            Assert.That(rig.Session.End.Step - k, Is.InRange(1, rcFrame), "disarmed at the first RC frame after Resume");

            PostFlight p = rig.Session.ReadSummary();
            TestContext.WriteLine((pausedAgainAfterMs > 0 ? "E3f " + pausedAgainAfterMs + " ms" : pluggedBackIn ? "E3b" : "E3a") + ": paused after step " + k + ", lost at host " + lostAt + " us; end at step " + rig.Session.End.Step + "; " + string.Join(" | ", p.Lines()));
            Assert.That((p.End.Kind, p.End.Step), Is.EqualTo((FlightEndKind.Failsafe, rig.Session.End.Step)));
            Assert.That(p.Log.Failsafes.Count, Is.EqualTo(1));
            Assert.That((p.Log.Failsafes[0].State.Step, p.Log.Failsafes[0].Stick.HostUs), Is.EqualTo((k + 1, lostAt)), "logged at the first step after Resume, with the loss's stamp");
            Assert.That(p.Log.Events.FindAll(e => e.Event.Kind == EventKind.Hitch && e.Event.Index == (byte)HitchCause.Pause).Count,
                Is.EqualTo(pausedAgainAfterMs > 0 ? 2 : 1), "each pause logged");
            StringAssert.StartsWith("failsafe", p.Cause());
            Assert.That(p.Lines().FindAll(l => l.StartsWith("failsafe at ")).Count, Is.EqualTo(1), "the summary lists the failsafe");

            ReplayResult replay;
            using (FileStream f = File.OpenRead(rig.Session.LogPath))
                replay = FlightReplay.Run(f, id => id == "floor" ? FlightWorld.Floor() : null, _spec.ContentHash);
            TestContext.WriteLine("replay: " + replay.Outcome + ", " + replay.Steps + " steps " + replay.Message);
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
        }

        // QA's probe Q2 (review, finding 14): armed at throttle 0.3, paused, the cable pulled, then Restart or End flight,
        // after the paused frames drained the loss or in the frame it came. Neither throws, and the loss is the failsafe:
        // the first RC frame after the pause runs before the end with the loss in force, so the log has it with its stamp
        // and the flight ends on it. Restart without the radio leaves the session Ended; the next flight starts once the
        // radio is back, with the switch to cycle again.
        [TestCase(true, true, TestName = "Q2: Restart after a radio lost during a pause logs the failsafe and waits for the radio")]
        [TestCase(false, true, TestName = "Q2: End flight after a radio lost during a pause logs the failsafe")]
        [TestCase(false, false, TestName = "Q2: End flight in the frame the radio is lost during a pause logs the failsafe")]
        public void EndingAPauseThatLostTheRadioIsTheFailsafe(bool restart, bool drained)
        {
            Rig rig = Make(i => i.Choices = new FlightChoices { Payload = 1.0, Legs = true });
            rig.Session.Start();
            rig.ArmCycle();
            rig.Radio.Throttle = 0.3;
            rig.Run(300);
            rig.Session.Pause();
            long k = rig.Session.Core.State.Step;
            rig.Run(100);
            rig.Radio.Lost = true;
            long lostAt;
            if (drained)
            {
                lostAt = rig.Clock.Now + FlightCore.StepUs; // the next pass sees it
                rig.Run(100);
            }
            else
            {
                rig.Clock.Now += FlightCore.StepUs; // the input thread's pass, and no frame since
                lostAt = rig.Clock.Now;
                rig.Plugged = null;
                rig.Reader.Pump(0);
            }
            Assert.That(rig.Reader.Connected, Is.False, "the cable is out");
            ulong seed = rig.Session.Seed;
            string log = rig.Session.LogPath;
            Double3 first = rig.Session.Setup.StartPosition;
            if (restart) Assert.That(rig.Session.Restart(), Is.False, "no radio: the new flight waits for it");
            else rig.Session.EndFlight();

            Assert.That((rig.Session.State, rig.Session.Flights), Is.EqualTo((SessionState.Ended, 1)));
            Assert.That(rig.Session.End.Kind, Is.EqualTo(FlightEndKind.Failsafe));
            long rcFrame = FlightController.LoopsPerRcFrame / FlightController.LoopsPerStep;
            Assert.That(rig.Session.End.Step - k, Is.InRange(1, rcFrame), "disarmed at the first RC frame after the pause");
            PostFlight p = rig.Session.ReadSummary();
            TestContext.WriteLine("Q2 " + (restart ? "Restart" : "End flight") + (drained ? "" : " in the loss's frame") + ": paused after step " + k + ", lost at host " + lostAt
                                  + " us; end at step " + rig.Session.End.Step + "; " + string.Join(" | ", p.Lines()));
            Assert.That((p.End.Kind, p.End.Step), Is.EqualTo((FlightEndKind.Failsafe, rig.Session.End.Step)));
            Assert.That(p.Log.Failsafes.Count, Is.EqualTo(1));
            Assert.That((p.Log.Failsafes[0].State.Step, p.Log.Failsafes[0].Stick.HostUs), Is.EqualTo((k + 1, lostAt)), "logged at the first step after the pause, with the loss's stamp");
            List<LoggedEvent> after = p.Log.Events.FindAll(e => e.Event.Kind == EventKind.Hitch && e.Step > k);
            Assert.That(after.ConvertAll(e => ((HitchCause)e.Event.Index, e.Step)), Is.EqualTo(new[] { (HitchCause.Pause, k + 1) }), "the pause logged, and no late pass");
            Assert.That(p.Lines().FindAll(l => l.StartsWith("failsafe at ")).Count, Is.EqualTo(1), "the summary lists the failsafe");
            ReplayResult replay;
            using (FileStream f = File.OpenRead(log))
                replay = FlightReplay.Run(f, id => id == "floor" ? FlightWorld.Floor() : null, _spec.ContentHash);
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
            if (!restart) return;

            var back = new TestRadio(rig.Clock) { Arm = true, Throttle = 0.3 };
            rig.Radio = back;
            rig.Plugged = back;
            for (int i = 0; i < 100 && !rig.Reader.Connected; i++) rig.Run(1);
            Assert.That(rig.Reader.Connected, Is.True, "back");
            rig.Session.Start(); // the host's, once the radio is in
            Assert.That((rig.Session.State, rig.Session.Flights), Is.EqualTo((SessionState.Flying, 2)));
            Assert.That(rig.Session.Seed, Is.Not.EqualTo(seed), "a new seed");
            Assert.That(rig.Session.LogPath, Is.Not.EqualTo(log), "a new log");
            Assert.That(rig.Session.Setup.StartPosition, Is.EqualTo(first), "from the start point");
            rig.Run(50);
            Assert.That(rig.Session.Core.State.Armed, Is.False, "the new flight needs the switch cycled");
        }

        // ---- Pause and restart ------------------------------------------------------------------------------------------

        [Test]
        public void PauseStopsTheStepsAndRestartIsANewFlight()
        {
            Rig rig = Make();
            rig.Session.Start();
            rig.ArmCycle();
            rig.Radio.Throttle = 0.3;
            rig.Run(300);
            FlightCore core = rig.Session.Core;
            long k = core.State.Step;
            rig.Session.Pause();
            rig.Run(2000);
            Assert.That(core.State.Step, Is.EqualTo(k), "no step while paused");
            Assert.That(rig.Session.State, Is.EqualTo(SessionState.Paused));
            rig.Session.Resume();
            rig.Run(300);
            Assert.That(core.State.Step, Is.EqualTo(k + 300), "stepping again, one a ms");
            Assert.That(rig.Session.PausedUs, Is.EqualTo(2000000));
            Double3 first = rig.Session.Setup.StartPosition;
            ulong seed = rig.Session.Seed;
            string log = rig.Session.LogPath;

            Assert.That(rig.Session.Restart(), Is.True, "the radio is in: the new flight starts at once");
            Assert.That(rig.Session.State, Is.EqualTo(SessionState.Flying));
            Assert.That(rig.Session.Flights, Is.EqualTo(2));
            Assert.That(rig.Session.Seed, Is.Not.EqualTo(seed), "a new seed");
            Assert.That(rig.Session.LogPath, Is.Not.EqualTo(log), "a new log");
            Assert.That(rig.Session.Setup.StartPosition, Is.EqualTo(first), "from the start point");
            Assert.That(rig.Session.Core.State.Step, Is.EqualTo(0));
            rig.Run(50);
            Assert.That(rig.Session.Core.State.Armed, Is.False, "the new flight needs the switch cycled too");

            // The first flight's log: complete, the pause in it, and every step's stick no later than its moved host time.
            FlightSummary s = FlightSummary.Read(log);
            Assert.That(s.Complete, s.CutReason);
            Assert.That(s.End.SticksDropped, Is.EqualTo(0), "the paused frames drained the radio's reports");
            LoggedEvent pause = s.Events.Find(e => e.Event.Kind == EventKind.Hitch && e.Event.Index == (byte)HitchCause.Pause);
            Assert.That(pause.Event.Kind, Is.EqualTo(EventKind.Hitch), "the pause is logged");
            Assert.That((pause.Step, pause.Event.B), Is.EqualTo((k + 1, 2000000.0)));
            TestContext.WriteLine("t = " + (pause.Step * FlightCore.Dt).ToString("0.000") + " s: " + s.Describe(pause.Event));
            using (FlightLogReader r = FlightLogReader.Open(log))
                while (r.Next())
                {
                    if (r.Kind != LogFormat.StepBlock) continue;
                    r.ReadStep(out LoggedStep step);
                    long index = step.State.Step - 1;
                    long hostUs = s.Header.StartHostUs + index * FlightCore.StepUs + (index >= k ? 2000000 : 0);
                    if (index < 30) continue; // before the radio's first report the stick is the neutral one
                    Assert.That(step.Stick.HostUs, Is.InRange(hostUs - 3 * FlightCore.StepUs, hostUs), "step " + index + "'s stick");
                }
            Assert.That(PostFlight.Read(log).End.Kind, Is.EqualTo(FlightEndKind.Pilot));
        }

        // ---- The end conditions -----------------------------------------------------------------------------------------

        // app-flow "A crash into the car", without the scene: a steel box named R01_A over the drone, full throttle.
        [Test]
        public void ACrashIntoR01ANamesThePartTheObjectAndTheMaterial()
        {
            Rig rig = Make(i => i.Adjust = s =>
            {
                ShapeGeometry box = Box(new Double3(0, 1.6, 0), new Double3(2, 0.2, 2), s.World.Query.MaterialId("steel"));
                var objects = new List<RuntimeObject>(s.RuntimeObjects) { new RuntimeObject("R01_A", new[] { box }) };
                s.RuntimeObjects = objects.ToArray();
            });
            rig.Session.Start();
            rig.ArmCycle();
            rig.Radio.Throttle = 1;
            rig.Run(3000);
            Assert.That(rig.Session.End.Kind, Is.EqualTo(FlightEndKind.Crash));

            PostFlight p = rig.Session.ReadSummary();
            TestContext.WriteLine(string.Join("\n", p.Lines()));
            Assert.That((p.End.Kind, p.End.Step), Is.EqualTo((FlightEndKind.Crash, rig.Session.End.Step)));
            Assert.That(p.HasCrash);
            Assert.That(p.CrashObject, Is.EqualTo("R01_A"));
            Assert.That(p.CrashMaterial, Is.EqualTo("steel"));
            Assert.That(Array.Exists(_spec.Parts, part => part.Name == p.CrashPart), "the part is drone.json's: " + p.CrashPart);
            StringAssert.Contains("R01_A", p.Cause());

            // The summary against the log and the flight: time, distance (the published poses' path), battery, seed.
            FlightCore core = rig.Session.Core;
            Assert.That(core.State.Step, Is.EqualTo(rig.Session.End.Step), "one step a tick, so the core stopped at the end");
            Assert.That(p.Seconds, Is.EqualTo(rig.Session.End.Seconds));
            Assert.That(p.Distance, Is.EqualTo(rig.Distance).Within(1e-9), "the distance");
            Assert.That(p.Distance, Is.GreaterThan(1));
            Assert.That(p.BatteryUsed, Is.EqualTo((2 - core.State.PackCharge0 - core.State.PackCharge1) / 2).Within(1e-15));
            Assert.That(p.Seed, Is.EqualTo(rig.Session.Seed));
            Assert.That(p.Lines(), Has.Member("log " + rig.Session.LogPath));
        }

        // app-flow "A crash into the car" on the test range (QA's finding 3): from the track start, turned to 108° to face
        // R01_A (17.5 m ahead and 4 m to the right), lifted, pitched forward and flown low into its flank. The summary
        // names the placed car by MapWorld.ObjectName, through the log's world id and hash; without the world, or with
        // another, it is "object N".
        [Test]
        public void ACrashIntoTheRangesCarNamesIt()
        {
            FlightWorldChoice range = FlightWorlds.TestRange(RepositoryRoot());
            Rig rig = Make(i =>
            {
                range.ApplyTo(i);
                i.Choices = new FlightChoices { StartPoint = "track", Legs = true, Payload = 1.0 };
                i.Adjust = s =>
                {
                    DetMath.SinCos(108 * Math.PI / 360, out double sin, out double cos);
                    s.StartAttitude = new Quat(0, sin, 0, cos);
                };
            });
            rig.Session.Start();
            rig.ArmCycle();
            for (int ms = 0; ms < 6000 && rig.Session.State != SessionState.Ended; ms += 10)
            {
                rig.Radio.Throttle = ms < 350 ? 0.62 : ms < 1000 ? 0.43 : 0.46; // lift, then low and level
                rig.Radio.Pitch = ms >= 350 && ms < 1000 ? 0.35 : 0; // forward, then held there (acro)
                rig.Run(10);
            }
            Assert.That(rig.Session.End.Kind, Is.EqualTo(FlightEndKind.Crash));

            PostFlight p = rig.Session.ReadSummary();
            TestContext.WriteLine(string.Join(" | ", p.Lines()));
            Assert.That(p.Log.Header.WorldId, Is.EqualTo("test-range"));
            Assert.That(p.HasCrash);
            Assert.That(p.CrashObject, Is.EqualTo("R01_A"));
            Assert.That(p.CrashMaterial, Is.EqualTo("sheet_metal"));
            Assert.That(Array.Exists(_spec.Parts, part => part.Name == p.CrashPart), "the part is drone.json's: " + p.CrashPart);
            StringAssert.Contains("hit R01_A (sheet_metal)", p.Cause());

            string unnamed = "object " + p.Crash.Event.Object;
            Assert.That(PostFlight.Read(rig.Session.LogPath).CrashObject, Is.EqualTo(unnamed), "no world to open");
            Assert.That(PostFlight.Read(rig.Session.LogPath, id => FlightWorld.Floor().Query).CrashObject, Is.EqualTo(unnamed), "another world's hash");

            // The log replays on the map it names.
            ReplayResult replay;
            using (FileStream f = File.OpenRead(rig.Session.LogPath))
                replay = FlightReplay.Run(f, id => id == range.Id ? range.Open() : null, _spec.ContentHash);
            TestContext.WriteLine("replay: " + replay.Outcome + ", " + replay.Steps + " steps " + replay.Message);
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
        }

        [Test]
        public void UpsideDownOnTheGroundEndsTheFlight()
        {
            Rig rig = Make(i => i.Adjust = s =>
            {
                double top = 0; // the highest contact point of the upright body, m
                foreach (ContactShape c in s.Contacts.Shapes)
                {
                    double up = c.Role == ContactRole.Disc ? c.HalfThickness : c.Radius;
                    top = Math.Max(top, Math.Max(c.A.Y, c.B.Y) + up);
                }
                s.StartAttitude = new Quat(0, 0, 1, 0); // rolled 180°
                s.StartPosition = new Double3(0, top + 0.002, 0);
            });
            rig.Session.Start();
            rig.Run(3000);
            Assert.That(rig.Session.End.Kind, Is.EqualTo(FlightEndKind.UpsideDown));
            PostFlight p = rig.Session.ReadSummary();
            TestContext.WriteLine(string.Join(" | ", p.Lines()));
            Assert.That((p.End.Kind, p.End.Step), Is.EqualTo((FlightEndKind.UpsideDown, rig.Session.End.Step)));
            Assert.That(p.End.Seconds, Is.GreaterThanOrEqualTo(FlightEndRule.UpsideDownSteps * FlightCore.Dt));
        }

        [Test]
        public void TheBatteryCutOffEndsTheFlight()
        {
            Rig rig = Make(i => i.Adjust = s => s.StartPackCharge = 0.0005);
            rig.Session.Start();
            rig.ArmCycle();
            rig.Radio.Throttle = 0.35;
            rig.Run(10000);
            Assert.That(rig.Session.End.Kind, Is.EqualTo(FlightEndKind.BatteryCutoff));
            FlightCore core = rig.Session.Core;
            Assert.That(Math.Min(core.State.PackCharge0, core.State.PackCharge1), Is.LessThanOrEqualTo(FlightEndRule.BatteryCutoffCharge));
            PostFlight p = rig.Session.ReadSummary();
            TestContext.WriteLine(string.Join(" | ", p.Lines()));
            Assert.That((p.End.Kind, p.End.Step), Is.EqualTo((FlightEndKind.BatteryCutoff, rig.Session.End.Step)));
        }

        // ---- Persistence ------------------------------------------------------------------------------------------------

        // app-flow "Settings survive a restart": the roll rate and the payload changed, the app quit and started again.
        [Test]
        public void TheRollRateAndThePayloadSurviveARestart()
        {
            string profile = Path.Combine(_dir, "profile");
            FcSettings edited = FcSettings.Default;
            edited.roll_srate = 30;
            FcSettingsStore.Save(profile, edited);
            SetupStore.Save(profile, new FlightChoices { Payload = 1.7, Legs = false, TimeOfDay = 5.25, Wind = WindLevel.Severe, Rain = Rain.Rain, StartPoint = "origin" });

            var problems = new List<string>();
            SessionInputs loaded = SessionInputs.FromProfile(profile, _spec, _materials, _materialsHash, problems);
            Assert.That(problems, Is.Empty);
            Assert.That(loaded.Settings.roll_srate, Is.EqualTo(30));
            FlightChoices c = loaded.Choices;
            Assert.That((c.Payload, c.Legs, c.TimeOfDay, c.Wind, c.Rain, c.StartPoint), Is.EqualTo((1.7, false, 5.25, WindLevel.Severe, Rain.Rain, "origin")));

            Rig rig = Make(i =>
            {
                i.Choices = loaded.Choices;
                i.Settings = loaded.Settings;
                i.Calibration = loaded.Calibration;
            });
            rig.Session.Start();
            rig.Run(50);
            rig.Session.EndFlight();
            LogHeader h = rig.Session.ReadSummary().Log.Header;
            h.ReadSettings(out FcSettings logged, new List<string>());
            Assert.That(logged.roll_srate, Is.EqualTo(30), "the flight flew the saved roll rate");
            Assert.That((h.Loadout.Payload, h.Loadout.Legs), Is.EqualTo((1.7, false)), "and the saved setup");

            File.WriteAllText(SetupStore.PathIn(profile), "{ not json");
            problems.Clear();
            loaded = SessionInputs.FromProfile(profile, _spec, _materials, _materialsHash, problems);
            Assert.That(problems.Count, Is.EqualTo(1), "a broken setup.json is named");
            Assert.That(loaded.Choices.Payload, Is.EqualTo(new FlightChoices().Payload), "and the default setup used");
            Assert.That(loaded.Settings.roll_srate, Is.EqualTo(30));
        }

        // ---- The keyboard -----------------------------------------------------------------------------------------------

        [Test]
        public void TheKeyboardFliesOnlyWithoutARadio()
        {
            Rig rig = Make(i => i.KeyboardAllowed = true, radio: false);
            rig.Session.Start();
            Assert.That(rig.Session.Input, Is.EqualTo(InputSource.Keyboard));
            rig.Run(30);
            rig.Run(1, KeyboardKeys.ArmToggle);
            rig.Run(100);
            FlightCore core = rig.Session.Core;
            Assert.That(core.State.Armed, Is.True, "Enter arms");
            rig.Run(500, KeyboardKeys.ThrottleUp);
            rig.Run(1, KeyboardKeys.ArmToggle);
            rig.Run(20);
            Assert.That(rig.Session.End.Kind, Is.EqualTo(FlightEndKind.Disarm));
            PostFlight p = rig.Session.ReadSummary();
            Assert.That(p.Log.Header.Input, Is.EqualTo(InputSource.Keyboard));
            Assert.That(p.Lines(), Has.Member("keyboard: never tune from this flight"));

            Rig radio = Make(i => i.KeyboardAllowed = true);
            radio.Session.Start();
            Assert.That(radio.Session.Input, Is.EqualTo(InputSource.Radio), "a radio is connected: the keyboard does not fly");
            for (int n = 0; n < 20; n++)
            {
                radio.Run(1, KeyboardKeys.ArmToggle);
                radio.Run(20);
            }
            Assert.That(radio.Session.Core.State.Armed, Is.False);

            Rig none = Make(radio: false);
            Assert.Throws<InvalidOperationException>(() => none.Session.Start(), "no radio, and no keyboard in the pilot's build");
        }

        // ---- The flight thread ------------------------------------------------------------------------------------------

        [Test]
        public void TheFlightThreadRunsInRealTimeAndPauses()
        {
            var inputs = Inputs();
            inputs.Clock = HostClock.Instance;
            inputs.Threaded = true;
            inputs.KeyboardAllowed = true;
            using (var session = new FlightSession(inputs))
            {
                session.Start();
                long t0 = HostClock.Instance.NowUs();
                Frames(session, 600000);
                session.Pause();
                long steps = session.Core.State.Step;
                Thread.Sleep(200);
                session.Tick();
                Assert.That(session.Core.State.Step, Is.EqualTo(steps), "no step while paused");
                session.Resume();
                Frames(session, 300000);
                session.EndFlight();
                long flown = HostClock.Instance.NowUs() - session.StartUs - session.PausedUs;
                FlightSummary s = FlightSummary.Read(session.LogPath);
                TestContext.WriteLine("on the flight thread: " + s.Steps + " steps in " + (flown / 1000) + " ms flown, a pause of " + (session.PausedUs / 1000)
                    + " ms, " + session.Hitches + " hitches; started " + ((session.StartUs - t0) / 1000.0).ToString("0.0") + " ms after Start returned");
                Assert.That(s.Complete, s.CutReason);
                Assert.That(s.Steps, Is.InRange(flown / 1000 - 30, flown / 1000 + 1), "about a step a ms");
                // No wall-time ceiling: a loaded host oversleeps (410 ms seen for the 200), and Resume moves a late schedule
                // further. The log decides: one Pause hitch, carrying exactly the shift the session applied.
                Assert.That(session.PausedUs, Is.GreaterThanOrEqualTo(190000), "the 200 ms sleep, less the timer's granularity");
                var pauses = s.Events.FindAll(e => e.Event.Kind == EventKind.Hitch && e.Event.Index == (byte)HitchCause.Pause);
                Assert.That(pauses.Count, Is.EqualTo(1), "the pause logged");
                Assert.That(pauses[0].Event.B, Is.EqualTo(session.PausedUs), "the log carries the pause the steps were moved by");
            }
        }

        static void Frames(FlightSession session, long us)
        {
            long until = HostClock.Instance.NowUs() + us;
            while (HostClock.Instance.NowUs() < until)
            {
                session.Tick();
                Thread.Sleep(4);
            }
        }

        // ---- The rig ----------------------------------------------------------------------------------------------------

        SessionInputs Inputs() => new SessionInputs
        {
            Drone = _spec,
            Materials = _materials,
            MaterialsHash = _materialsHash,
            LogFolder = Path.Combine(_dir, "flights"),
            Build = "inputtests",
        };

        Rig Make(Action<SessionInputs> edit = null, bool radio = true)
        {
            var rig = new Rig { Clock = new ManualClock { Now = 1000000 } };
            if (radio) rig.Plugged = rig.Radio = new TestRadio(rig.Clock);
            rig.Reader = new RadioReader(() => rig.Plugged);
            SessionInputs inputs = Inputs();
            inputs.Reader = rig.Reader;
            inputs.Clock = rig.Clock;
            inputs.Threaded = false;
            ulong seed = 0x5E55;
            inputs.NewSeed = () => ++seed;
            edit?.Invoke(inputs);
            rig.Session = new FlightSession(inputs);
            rig.Run(10); // the radio opens and reports
            return rig;
        }

        sealed class Rig
        {
            public ManualClock Clock;
            public TestRadio Radio;
            public IRadioDevice Plugged;
            public RadioReader Reader;
            public FlightSession Session;
            public double Distance;
            long _lastStep = -1;
            Double3 _last;

            /// <summary>ms of the input thread's passes and the session's frames, a pass and a frame each ms; stops at the end.</summary>
            public void Run(int ms, KeyboardKeys keys = KeyboardKeys.None)
            {
                for (int i = 0; i < ms; i++)
                {
                    Clock.Now += FlightCore.StepUs;
                    if (Radio != null && Radio.Lost && Plugged == Radio) Plugged = null; // the cable stays out
                    Reader.Pump(0);
                    Session.Tick(keys);
                    if (Session.State == SessionState.Flying || Session.State == SessionState.Ended) Track();
                    if (Session.State == SessionState.Ended) return;
                }
            }

            /// <summary>The switch off, then on with the throttle down.</summary>
            public void ArmCycle()
            {
                Radio.Throttle = 0;
                Radio.Arm = false;
                Run(30);
                Radio.Arm = true;
                Run(30);
                Assert.That(Session.Core.State.Armed, "armed");
            }

            // The published poses' path, step by step.
            void Track()
            {
                ref readonly PosePair pair = ref Session.Poses.Read();
                if (pair.Current.Step == _lastStep) return;
                if (_lastStep >= 0 && pair.Current.Step == _lastStep + 1) Distance += (pair.Current.Position - _last).Length();
                _lastStep = pair.Current.Step;
                _last = pair.Current.Position;
            }
        }

        sealed class ManualClock : IHostClock
        {
            public long Now;
            public long NowUs() => Now;
        }

        // The TX12's layout (8 axes 0–2047, AETR and the arm on ch5, RawStickMap's), reporting every 2 ms on the clock.
        sealed class TestRadio : IRadioDevice
        {
            public const string DeviceName = "RadioMaster TX12 (test)";

            static readonly RadioAxis[] Tx12 =
            {
                new RadioAxis(1, 0x30, 0, 2047), new RadioAxis(1, 0x31, 0, 2047), new RadioAxis(1, 0x32, 0, 2047), new RadioAxis(1, 0x33, 0, 2047),
                new RadioAxis(1, 0x34, 0, 2047), new RadioAxis(1, 0x35, 0, 2047), new RadioAxis(1, 0x36, 0, 2047), new RadioAxis(1, 0x36, 0, 2047),
            };

            readonly ManualClock _clock;
            long _next = long.MinValue;
            public double Throttle; // 0–1
            public double Pitch; // −1–1; roll and yaw centred
            public bool Arm, Lost;

            public TestRadio(ManualClock clock) => _clock = clock;

            public string Name => DeviceName;
            public InputSource Source => InputSource.Radio;
            public RadioAxis[] Axes => Tx12;
            public int ButtonCount => 24;

            public RadioRead Read(RadioReport report, int timeoutMs)
            {
                report.HostUs = _clock.Now;
                if (Lost) return RadioRead.Lost;
                if (_clock.Now < _next) return RadioRead.Timeout;
                _next = _clock.Now + 2000;
                report.Axes[0] = report.Axes[3] = 1024;
                report.Axes[1] = (int)Math.Round(1023.5 + Pitch * 1023.5);
                report.Axes[2] = (int)Math.Round(Throttle * 2047);
                report.Axes[4] = Arm ? 2047 : 0;
                for (int i = 5; i < 8; i++) report.Axes[i] = 1024;
                report.Buttons = 0;
                return RadioRead.Report;
            }

            public void Dispose()
            {
            }
        }

        static ShapeGeometry Box(Double3 centre, Double3 half, ushort material) => new ShapeGeometry
        {
            Kind = ShapeKind.Box, Center = centre, AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0), AxisZ = new Double3(0, 0, 1),
            HalfExtents = half, Material = material,
        };

        static string RepositoryRoot() =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(RepositoryFile("maps/test-range/placements.json")), "..", ".."));

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
            Assert.Fail(relative + " was not found up from " + here);
            return null;
        }
    }
}
