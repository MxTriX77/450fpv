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

        // The airframe's modes on letter T's layout (L4, 1 kg): the arms' four at the builder's notches (fc-profiles §1: the
        // gyro's at 50 Hz, the D term's at 60, the dynamic one from 60), the coil's mount and the payload's sling below.
        [Test]
        public void TheModesSitAtTheBuildersNotches()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            var core = new FlightCore(ShakeScenario.Setup(spec, 1, 1));
            AirframeModes m = core.Modes;
            Assert.That(m.Count, Is.EqualTo(10), "four arms, the payload's three and the coil's three");
            for (int i = 0; i < m.Count; i++) TestContext.WriteLine($"mode {i}: {m.Frequency(i):F2} Hz, ζ {m.Damping(i):F3}");
            Assert.That(m.Frequency(0), Is.InRange(1.5, 2.5), "the payload's sling swings as its pendulum: √(g/ℓ)/2π at ℓ 0.105 m is 1.54 Hz on a fixed pivot, higher with the body free");
            for (int i = m.Count - 4; i < m.Count; i++) Assert.That(m.Frequency(i), Is.InRange(45, 65), "an arm mode");
            var empty = new FlightCore(ShakeScenario.Setup(spec, 0, 1));
            Assert.That(empty.Modes.Count, Is.EqualTo(7), "no payload, no sling");
        }

        // Fifty arrested descents: two payloads, four drops (from the hover, 2.5, 5.4 and 9 m/s), a sharp full and a sharp
        // 0.8 step and the gentle ramp, two seeds; and the known-wrong rigid airframe twice.
        [Test]
        public void ShakeOnlyWhenItsCausesMeet()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            int flights = 0;
            foreach (double payload in Payloads)
                foreach (double drop in Drops)
                {
                    var peaks = new double[3, Seeds.Length];
                    for (int s = 0; s < Seeds.Length; s++)
                        for (int input = 0; input < 3; input++)
                        {
                            bool gentle = input == 2;
                            double stick = input == 0 ? 1 : 0.8;
                            ShakeScenario.Result r = ShakeScenario.Fly(ShakeScenario.Setup(spec, payload, Seeds[s]), drop, gentle, stick);
                            flights++;
                            peaks[input, s] = r.Peak;
                            TestContext.WriteLine($"{payload:F0} kg, drop {drop:F1} s, {(gentle ? "gentle" : "step " + stick.ToString("F1"))}, seed {Seeds[s]}: "
                                + ShakeScenario.Describe(r));
                            Assert.That(r.Published, Is.True, "the camera's vibration reaches the feed");
                            if (drop == 0)
                            {
                                // From the hover: a gentle increase never shows it; a sharp one only its own kick as the motors
                                // spin up, under 0.3 s, the ESCs off their limit within 0.1 s.
                                if (gentle) Assert.That(r.Fields, Is.EqualTo(0), "a gentle increase never shows it");
                                else Assert.That(r.Duration, Is.LessThan(0.3), "a punch from the hover is a kick, not the shake");
                                Assert.That(r.Limited, Is.LessThan(0.1));
                                continue;
                            }
                            if (gentle) continue;
                            // Arresting a descent sharply: the shake, seen mainly on the camera, as long as the arrest holds the
                            // ESCs on their limit and the rotors in their wash.
                            Assert.That(r.Duration, Is.GreaterThanOrEqualTo(0.28), "the shake after the arrest");
                            Assert.That(r.InRing, Is.GreaterThan(0.2), "the rotors re-ingest their wash");
                            Assert.That(r.VibrationPeak.Y, Is.GreaterThan(0.5), "the frame's band rings");
                        }
                    for (int s = 0; s < Seeds.Length; s++)
                        if (drop > 0) Assert.That(peaks[2, s], Is.LessThan(peaks[0, s]), "the gentle input's jitter is below the sharp punch's at the same descent");
                    if (drop > 0) Assert.That(peaks[0, 0], Is.Not.EqualTo(peaks[0, 1]), "it varies between occurrences");
                }
            // Known-wrong: a rigid airframe, the modes off. The camera then has no frame to ring and the feed no vibration.
            foreach (ulong seed in Seeds)
            {
                ShakeScenario.Result r = ShakeScenario.Fly(ShakeScenario.Setup(spec, 2, seed, false), 0.6, false);
                flights++;
                TestContext.WriteLine($"rigid, 2 kg, drop 0.6 s, step 1.0, seed {seed}: " + ShakeScenario.Describe(r));
                Assert.That(r.Published, Is.False, "the known-wrong rigid airframe publishes no camera vibration");
            }
            Assert.That(flights, Is.EqualTo(50));
        }

        // The no-payload drone arrested sharply from 2.5–5.4 m/s, the pilot's PR-1: the shake lasts 0.3–0.75 s.
        [Test]
        public void TheShakeLastsAboutTheArrest()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (double drop in new[] { 0.3, 0.6 })
                foreach (ulong seed in Seeds)
                    foreach (double stick in new[] { 1.0, 0.8 })
                    {
                        ShakeScenario.Result r = ShakeScenario.Fly(ShakeScenario.Setup(spec, 0, seed), drop, false, stick);
                        Assert.That(r.Duration, Is.InRange(0.28, 0.75), ShakeScenario.Describe(r));
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
