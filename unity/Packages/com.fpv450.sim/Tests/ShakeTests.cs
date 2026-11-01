using System.IO;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // airframe-effects: "The throttle-punch shake arises from its causes" (PHY-10, PR-1; p7b-airframe 1.4).
    public class ShakeTests
    {
        static readonly double[] Payloads = { 0, 2 }, Drops = { 0, 0.3, 0.6, 1.0 };
        static readonly ulong[] Seeds = { 1, 2 };

        // The airframe's modes on D-050's layout (1 kg): the arms' four at the builder's notches (fc-profiles §1: the gyro's
        // at 50 Hz, the D term's at 60, the dynamic one from 60), below them the bottle's strapped mount on top and the coil's
        // beneath, no sling.
        [Test]
        public void TheModesSitAtTheBuildersNotches()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            var core = new FlightCore(ShakeScenario.Setup(spec, 1, 1));
            AirframeModes m = core.Modes;
            Assert.That(m.Count, Is.EqualTo(10), "four arms, the bottle's three and the coil's three");
            for (int i = 0; i < m.Count; i++) TestContext.WriteLine($"mode {i}: {m.Frequency(i):F2} Hz, ζ {m.Damping(i):F3}");
            for (int i = 0; i < 6; i++)
                Assert.That(m.Frequency(i), Is.InRange(27, 37), "the loads' strapped mounts, the bottle's and the full coil's, 1 kg each: √(k/m)/2π at 30 kN/m is 27.6 Hz on a fixed base, higher with the body free");
            for (int i = m.Count - 4; i < m.Count; i++) Assert.That(m.Frequency(i), Is.InRange(45, 65), "an arm mode");
            var empty = new FlightCore(ShakeScenario.Setup(spec, 0, 1));
            Assert.That(empty.Modes.Count, Is.EqualTo(7), "no bottle: the arms and the coil");
        }

        // Fifty arrested descents: two payloads, four drops (from the hover, 2.5, 5.4 and 9 m/s), a sharp full and a sharp
        // 0.8 step and the gentle ramp, two seeds; and the known-wrong rigid airframe twice. The spec's three scenarios (D-049):
        // "Shake only when its causes meet", "The shake lasts as long as the arrest", "The visible shake is the rigid body's".
        [Test]
        public void ShakeOnlyWhenItsCausesMeet()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            int flights = 0;
            var heavyPunch = new ShakeScenario.Result[Seeds.Length];
            foreach (double payload in Payloads)
                foreach (double drop in Drops)
                {
                    var runs = new ShakeScenario.Result[3, Seeds.Length];
                    for (int s = 0; s < Seeds.Length; s++)
                        for (int input = 0; input < 3; input++)
                        {
                            bool gentle = input == 2;
                            double stick = input == 0 ? 1 : 0.8;
                            ShakeScenario.Result r = ShakeScenario.Fly(ShakeScenario.Setup(spec, payload, Seeds[s]), drop, gentle, stick);
                            flights++;
                            runs[input, s] = r;
                            string at = $"{payload:F0} kg, drop {drop:F1} s, {(gentle ? "gentle" : "step " + stick.ToString("F1"))}, seed {Seeds[s]}: "
                                + ShakeScenario.Describe(r);
                            TestContext.WriteLine(at);
                            Assert.That(r.Published, Is.True, "the camera's vibration reaches the feed");
                            if (drop == 0)
                            {
                                // From the hover: a gentle increase never shows it; a sharp one only its own kick as the motors
                                // spin up, under 0.3 s, the ESCs off their limit within 0.1 s.
                                if (gentle) Assert.That(r.Fields, Is.EqualTo(0), "a gentle increase never shows it: " + at);
                                else Assert.That(r.Duration, Is.LessThan(0.3), "a punch from the hover is a kick, not the shake: " + at);
                                Assert.That(r.Limited, Is.LessThan(0.1), at);
                                continue;
                            }
                            if (gentle)
                            {
                                // In a 5–9 m/s descent a gentle ramp shows PHY-09's wobble instead (the 2.5 m/s one is not asked).
                                if (drop >= 0.6) Assert.That(ShakeScenario.Wobbles(r), Is.True, "the rotors settle into their wash: " + at);
                                continue;
                            }
                            // Arresting a descent sharply: the shake, the rigid body jittering and tilting uncommanded while the
                            // ESCs sit at their limit and the rotors re-ingest their wash; the frame's modes add the 50–200 Hz band.
                            Assert.That(r.Duration, Is.GreaterThanOrEqualTo(0.28), "the shake after the arrest: " + at);
                            Assert.That(r.Limited, Is.GreaterThan(0.25), "the ESCs sit at their limit: " + at);
                            Assert.That(r.InRing, Is.GreaterThan(0.2), "the rotors re-ingest their wash: " + at);
                            Assert.That(r.TiltMax, Is.InRange(5, 16), "the tilt uncommanded: " + at);
                            Assert.That(r.VibrationPeak.Y, Is.GreaterThan(0.5), "the frame's band rings: " + at);
                            // It lasts as long as the arrest: 0.3–0.7 s for the empty drone's moderate descents; a full punch
                            // holds the ESCs on their limit through the whole arrest, 0.8–1.4 s with the payload or from 9 m/s.
                            if (payload == 0 && drop <= 0.6) Assert.That(ShakeScenario.LastsInTenths(r.Duration, 0.3, 0.7), Is.True, at);
                            if (stick < 1) continue;
                            Assert.That(ShakeScenario.LastsTheArrest(r), Is.True, "a full punch's shake lasts its arrest: " + at);
                            if (payload > 0 || drop >= 1) Assert.That(ShakeScenario.LastsInTenths(r.Duration, 0.8, 1.4), Is.True, at);
                            if (payload > 0 && drop == 0.6) heavyPunch[s] = r;
                        }
                    if (drop == 0) continue;
                    for (int s = 0; s < Seeds.Length; s++)
                    {
                        // A smaller step lets the ESCs go sooner, and its shake is shorter; the gentle ramp's below the punch's.
                        Assert.That(runs[1, s].Limited, Is.LessThan(runs[0, s].Limited), "the 0.8 step's ESCs leave their limit sooner");
                        Assert.That(runs[1, s].Duration, Is.LessThan(runs[0, s].Duration), "the 0.8 step's shake is shorter");
                        Assert.That(runs[2, s].Peak, Is.LessThan(runs[0, s].Peak), "the gentle input's jitter is below the sharp punch's at the same descent");
                    }
                    Assert.That(runs[0, 0].Peak, Is.Not.EqualTo(runs[0, 1].Peak), "it varies between occurrences");
                }
            // Known-wrong: a rigid airframe, the modes off. The visible shake is the rigid body's, so it stays (within 20 % of
            // the flexing airframe's, as long); but the camera has no frame to ring, and the feed no vibration.
            for (int s = 0; s < Seeds.Length; s++)
            {
                ShakeScenario.Result r = ShakeScenario.Fly(ShakeScenario.Setup(spec, 2, Seeds[s], false), 0.6, false);
                flights++;
                string at = $"rigid, 2 kg, drop 0.6 s, step 1.0, seed {Seeds[s]}: " + ShakeScenario.Describe(r);
                TestContext.WriteLine(at);
                Assert.That(r.Peak / heavyPunch[s].Peak, Is.InRange(0.8, 1.2), "the visible shake is the rigid body's: " + at);
                Assert.That(ShakeScenario.LastsTheArrest(r) && ShakeScenario.LastsInTenths(r.Duration, 0.8, 1.4), Is.True, at);
                Assert.That(r.Published, Is.False, "the known-wrong rigid airframe publishes no camera vibration");
            }
            Assert.That(flights, Is.EqualTo(50));
        }

        // The no-payload drone arrested sharply from 2.5–5.4 m/s, the pilot's PR-1: the shake lasts 0.3–0.7 s (D-049).
        [Test]
        public void TheShakeLastsAboutTheArrest()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (double drop in new[] { 0.3, 0.6 })
                foreach (ulong seed in Seeds)
                    foreach (double stick in new[] { 1.0, 0.8 })
                    {
                        ShakeScenario.Result r = ShakeScenario.Fly(ShakeScenario.Setup(spec, 0, seed), drop, false, stick);
                        Assert.That(ShakeScenario.LastsInTenths(r.Duration, 0.3, 0.7), Is.True, ShakeScenario.Describe(r));
                    }
        }

        [Test]
        public void ARecordedPunchReplaysBitForBit()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = ShakeScenario.Setup(spec, 1, 1);
            var core = new FlightCore(setup);
            LogHeader h = LogHeader.For(setup, core);
            h.Build = "simtests";
            h.Isa = "test";
            h.WorldId = "floor";
            h.SetDrone(spec, new Loadout { Payload = 1, Legs = true });
            var log = new MemoryStream();
            var recorder = new FlightRecorder(log, h, FlightRecorder.DefaultCapacity, ownsStream: false);
            ShakeScenario.Result r = ShakeScenario.Fly(core, setup, 0.6, false, 1, (c, stick) => recorder.RecordStep(c.State, stick, c.Trace, c.Events));
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            log.Position = 0;
            ReplayResult replay = FlightReplay.Run(log, id => id == "floor" ? FlightWorld.Floor() : null, spec.ContentHash);
            TestContext.Out.WriteLine(replay.Message + ": " + replay.Steps + " steps, " + replay.Events + " events; " + ShakeScenario.Describe(r));
            Assert.That(r.Fields, Is.GreaterThan(0));
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
        }
    }
}
