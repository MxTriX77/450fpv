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

        // Sixty-two arrested descents: two payloads, four drops (from the hover, 2.5, 5.4 and 9 m/s), a sharp full and a sharp
        // 0.8 step and the gentle ramp, two seeds; the known-wrong ESC that senses no current on each full punch, and the
        // known-wrong rigid airframe twice. The spec's three scenarios (D-049, D-051), each checked as it holds on every
        // flight of 40 seeds a cell (flightbench --shake): "Shake only when its causes meet", "The shake lasts as long as the
        // arrest", "The visible shake is the rigid body's".
        [Test]
        public void ShakeOnlyWhenItsCausesMeet()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            int flights = 0;
            double halfSum = 0, fullSum = 0;
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
                            if (gentle) continue;
                            // Arresting a descent sharply shows it, an ESC meeting its current limit, and the frame's band rings.
                            Assert.That(r.Fields, Is.GreaterThan(0), "every sharp arrest shows it: " + at);
                            Assert.That(r.Limited, Is.GreaterThan(0), "an ESC meets its limit: " + at);
                            Assert.That(r.VibrationPeak.Y, Is.GreaterThan(ShakeScenario.Rings), "the frame's band rings: " + at);
                            if (stick < 1) continue;
                            // A full punch holds an ESC on its limit through the whole arrest, all four for part of it, and its
                            // shake shows while it lasts and never outlasts it by more than the loop's settling.
                            Assert.That(ShakeScenario.LastsTheArrest(r), Is.True, "a full punch's shake lasts its arrest: " + at);
                            if (payload > 0 && drop == 0.6) heavyPunch[s] = r;
                        }
                    if (drop == 0) continue;
                    for (int s = 0; s < Seeds.Length; s++)
                    {
                        ShakeScenario.Result full = runs[0, s], half = runs[1, s], gentle = runs[2, s];
                        // A smaller step lets the ESCs go sooner; in a 5–9 m/s descent a gentle ramp shows PHY-09's wobble.
                        Assert.That(half.Limited, Is.LessThan(full.Limited), "the 0.8 step's ESCs leave their limit sooner");
                        halfSum += half.Duration;
                        fullSum += full.Duration;
                        if (drop >= 0.6) Assert.That(ShakeScenario.Wobbles(gentle, full), Is.True, "the rotors settle into their wash: " + ShakeScenario.Describe(gentle));
                        // Known-wrong: an ESC that senses no current (D-051). Nothing holds the loop short of authority: its
                        // tilt falls below half the full punch's, and no ESC sits on a limit through the arrest.
                        ShakeScenario.Result wrong = ShakeScenario.Fly(ShakeScenario.Setup(spec, payload, Seeds[s], currentLimit: false), drop, false);
                        flights++;
                        string w = $"no current limit, {payload:F0} kg, drop {drop:F1} s, seed {Seeds[s]}: " + ShakeScenario.Describe(wrong);
                        TestContext.WriteLine(w);
                        Assert.That(ShakeScenario.LastsTheArrest(wrong), Is.False, w);
                        Assert.That(wrong.TiltMax, Is.LessThan(0.5 * full.TiltMax), "the tilt is the current limit's: " + w);
                    }
                    Assert.That(runs[0, 0].Peak, Is.Not.EqualTo(runs[0, 1].Peak), "it varies between occurrences");
                }
            // The 0.8 step's shake is shorter: over these 12 pairs in all (pair by pair on 229 of 240 on 40 seeds a cell).
            TestContext.WriteLine($"the 0.8 step's shakes over the full punches': {halfSum / fullSum:F2}");
            Assert.That(halfSum, Is.LessThan(fullSum), "the 0.8 step's shake is shorter");
            // Known-wrong: a rigid airframe, the modes off. The visible shake is the rigid body's, so it stays (within 20 % of
            // the flexing airframe's, as long); but the camera has no frame to ring, and the feed no vibration.
            for (int s = 0; s < Seeds.Length; s++)
            {
                ShakeScenario.Result r = ShakeScenario.Fly(ShakeScenario.Setup(spec, 2, Seeds[s], false), 0.6, false);
                flights++;
                string at = $"rigid, 2 kg, drop 0.6 s, step 1.0, seed {Seeds[s]}: " + ShakeScenario.Describe(r);
                TestContext.WriteLine(at);
                Assert.That(r.Peak / heavyPunch[s].Peak, Is.InRange(0.8, 1.2), "the visible shake is the rigid body's: " + at);
                Assert.That(ShakeScenario.LastsTheArrest(r), Is.True, at);
                Assert.That(System.Math.Abs(r.Duration - heavyPunch[s].Duration), Is.LessThanOrEqualTo(3 * FlightCore.FieldSteps * FlightCore.Dt + 1e-9), "as long, within three fields (0.06 s on 40 seeds at most): " + at);
                Assert.That(r.Published, Is.False, "the known-wrong rigid airframe publishes no camera vibration");
            }
            Assert.That(flights, Is.EqualTo(62));
        }

        // The empty drone's full punch from 2.5 and 5.3 m/s on seeds 1–10, the pilot's PR-1: the shake lasts 0.3–0.7 s on
        // most (77 of 80 on seeds 1–40; the rest are weak shakes over the visible line for 0.1–0.2 s), its median in it. The
        // known-wrong ESC that senses no current fails it from 2.5 m/s, where its shake barely shows (1 of 40 in the band).
        [Test]
        public void TheShakeLastsAboutTheArrest()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (double drop in new[] { 0.3, 0.6 })
                foreach (bool limit in new[] { true, false })
                {
                    if (!limit && drop > 0.3) continue;
                    var lengths = new double[10];
                    int inside = 0;
                    for (int s = 0; s < lengths.Length; s++)
                    {
                        ShakeScenario.Result r = ShakeScenario.Fly(ShakeScenario.Setup(spec, 0, (ulong)(s + 1), currentLimit: limit), drop, false);
                        lengths[s] = r.Duration;
                        if (ShakeScenario.LastsInTenths(r.Duration, 0.3, 0.7)) inside++;
                    }
                    System.Array.Sort(lengths);
                    double median = (lengths[4] + lengths[5]) / 2;
                    string at = $"drop {drop:F1} s{(limit ? "" : ", no current limit")}: {inside} of 10 in 0.3–0.7 s, {lengths[0]:F2}–{lengths[9]:F2} s, median {median:F2} s";
                    TestContext.WriteLine(at);
                    Assert.That(inside >= 8 && ShakeScenario.LastsInTenths(median, 0.3, 0.7), Is.EqualTo(limit), at);
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
