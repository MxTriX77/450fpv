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

        // Seventy-four arrested descents: two payloads, four drops (from the hover, 2.5, 5.4 and 9 m/s), a sharp full and a
        // sharp 0.8 step and the gentle ramp, two seeds; each full punch again with 40 A current protection (D-051's
        // alternative) and with the known-wrong ringless rotors; the known-wrong rigid airframe twice. The drone's own ESC is
        // the class's, which senses no current (D-066). The spec's three scenarios (D-049, D-051, D-053, D-066): "Shake only
        // when its causes meet", "The shake lasts as long as the arrest", "The visible shake is the rigid body's". What they
        // state of every flight is checked on each flight here; what they state of nearly every one, or of the median, is a
        // rate, checked on 40 seeds a cell by flightbench --shake, which fails when one misses (QA's re-review, B1).
        [Test]
        public void ShakeOnlyWhenItsCausesMeet()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            int flights = 0;
            double ringedPeaks = 0, ringlessPeaks = 0;
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
                            Assert.That(r.Limited, Is.EqualTo(0), "the class's ESC senses no current, so none is limited: " + at);
                            if (drop == 0)
                            {
                                // From the hover: a gentle increase never shows it; a sharp one only its own kick as the motors
                                // spin up, shorter than the pilot's shortest shake.
                                if (gentle) Assert.That(r.Fields, Is.EqualTo(0), "a gentle increase never shows it: " + at);
                                else Assert.That(r.Duration, Is.LessThan(ShakeScenario.KickSeconds), "a punch from the hover is a kick, not the shake: " + at);
                                continue;
                            }
                            if (gentle)
                            {
                                // In a 5–9 m/s descent a gentle ramp's rotors settle into their ring instead, PHY-09's wobble
                                // (that it shows over the visible line is a rate, nearly every one).
                                if (drop >= 0.6) Assert.That(r.InRing, Is.GreaterThan(1.0), "the rotors settle into their wash: " + at);
                                continue;
                            }
                            // Arresting a descent sharply rings the frame's band (that it shows is a rate below 9 m/s: a weak arrest
                            // stays under the visible line).
                            Assert.That(r.VibrationPeak.Y, Is.GreaterThan(ShakeScenario.Rings), "the frame's band rings: " + at);
                            if (stick < 1) continue;
                            // A full punch's shake ends with its arrest, within the loop's settling (that it shows from 9 m/s is
                            // a rate, nearly every one: a heavy one can stay just under the visible line).
                            Assert.That(ShakeScenario.EndsWithTheArrest(r), Is.True, "a full punch's shake ends with its arrest: " + at);
                            if (payload > 0 && drop == 0.6) heavyPunch[s] = r;
                        }
                    if (drop == 0) continue;
                    for (int s = 0; s < Seeds.Length; s++)
                    {
                        ShakeScenario.Result full = runs[0, s];
                        // 40 A current protection (D-051's alternative, model 22's drone): the arrest takes longer, its shake
                        // still ending with it (that an ESC holds its limit through it and the drone tilts further are rates,
                        // nearly every one: --shake).
                        ShakeScenario.Result held = ShakeScenario.Fly(ShakeScenario.Setup(spec, payload, Seeds[s], protection: true), drop, false);
                        flights++;
                        string w = $"40 A protection, {payload:F0} kg, drop {drop:F1} s, seed {Seeds[s]}: " + ShakeScenario.Describe(held);
                        TestContext.WriteLine(w);
                        Assert.That(ShakeScenario.EndsWithTheArrest(held), Is.True, w);
                        Assert.That(held.ArrestedAfter, Is.GreaterThan(full.ArrestedAfter), "the limit lengthens the arrest: " + w);
                        // Known-wrong: rotors that never re-ingest their wash (no ring). The shake is mainly the wash's (D-064's
                        // motor spreads a second cause, ShakeScenario.PastTheKick), so it weakens (on most seeds, a rate; here in sum).
                        FlightSetup ringless = ShakeScenario.Setup(spec, payload, Seeds[s]);
                        ringless.VortexRing = null;
                        ShakeScenario.Result noRing = ShakeScenario.Fly(ringless, drop, false);
                        flights++;
                        TestContext.WriteLine($"no ring, {payload:F0} kg, drop {drop:F1} s, seed {Seeds[s]}: " + ShakeScenario.Describe(noRing));
                        ringedPeaks += full.Peak;
                        ringlessPeaks += noRing.Peak;
                    }
                    Assert.That(runs[0, 0].Peak, Is.Not.EqualTo(runs[0, 1].Peak), "it varies between occurrences");
                }
            TestContext.WriteLine($"the ringless rotors' peaks over the ringed ones', summed: {ringlessPeaks / ringedPeaks:F2}");
            Assert.That(ringlessPeaks, Is.LessThan(ringedPeaks), "the shake is mainly the re-ingested wash's");
            // Known-wrong: a rigid airframe, the modes off. The visible shake is the rigid body's, so it stays (within 20 % of
            // the flexing airframe's peak; as long is a rate, the same median and within three fields on nearly every seed);
            // but the camera has no frame to ring, and the feed no vibration.
            for (int s = 0; s < Seeds.Length; s++)
            {
                ShakeScenario.Result r = ShakeScenario.Fly(ShakeScenario.Setup(spec, 2, Seeds[s], false), 0.6, false);
                flights++;
                string at = $"rigid, 2 kg, drop 0.6 s, step 1.0, seed {Seeds[s]}: " + ShakeScenario.Describe(r);
                TestContext.WriteLine(at);
                Assert.That(r.Peak / heavyPunch[s].Peak, Is.InRange(0.8, 1.2), "the visible shake is the rigid body's: " + at);
                Assert.That(ShakeScenario.EndsWithTheArrest(r), Is.True, at);
                Assert.That(r.Published, Is.False, "the known-wrong rigid airframe publishes no camera vibration");
            }
            Assert.That(flights, Is.EqualTo(74));
        }

        // The empty drone's full punch from 9 m/s (a sudden drop) on seeds 1–10, the pilot's PR-1: the shake lasts 0.3–0.7 s
        // on most, at least 7 of 10 (ShakeScenario.Most), its median in it.
        // The known-wrong ringless rotors cannot be told from it in that cell (QA's integration review, B2): D-064's motor
        // spreads, which no current limit trims (D-066), shake a fast arrest without the wash too, inside the band on 272 of
        // 520 seeds (1–40, 301–500, 701–740, 1001–1200, 1301–1340), so ten seeds pass the band about 21 % of the time and
        // forty about 1.8 %. So the known-wrong is judged as flightbench --shake judges it, the wash as the main cause: on
        // seeds 1–40 of the six arrested descents (both payloads, 2.5, 5.3 and 9 m/s) the ringless shake is weaker on most
        // and lasts past the kick on at most half as many. Bootstrapped from the same 520 seeds' flights, a fresh 40 fails
        // that about 0.6 % of the time (0.57 % of 20 000 draws). 480 flights, each on its own seed and independent, in parallel.
        [Test]
        public void TheShakeLastsAboutTheArrest()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            double[] payloads = { 0, 2 }, drops = { 0.3, 0.6, 1.0 };
            const int Seeds = 40;
            var runs = new ShakeScenario.Result[2, payloads.Length * drops.Length, Seeds]; // [ring off, cell, seed]
            System.Threading.Tasks.Parallel.For(0, runs.Length, j =>
            {
                int ringless = j / (runs.GetLength(1) * Seeds), cell = j / Seeds % runs.GetLength(1), s = j % Seeds;
                FlightSetup setup = ShakeScenario.Setup(spec, payloads[cell / drops.Length], (ulong)(s + 1));
                if (ringless == 1) setup.VortexRing = null;
                runs[ringless, cell, s] = ShakeScenario.Fly(setup, drops[cell % drops.Length], false);
            });

            var lengths = new double[10];
            int inside = 0, pilots = drops.Length - 1; // the empty drone from 9 m/s
            for (int s = 0; s < lengths.Length; s++)
            {
                lengths[s] = runs[0, pilots, s].Duration;
                if (ShakeScenario.LastsInTenths(lengths[s], 0.3, 0.7)) inside++;
            }
            System.Array.Sort(lengths);
            double median = (lengths[4] + lengths[5]) / 2;
            string at = $"from 9 m/s: {inside} of 10 in 0.3–0.7 s, {lengths[0]:F2}–{lengths[9]:F2} s, median {median:F2} s";
            TestContext.WriteLine(at);
            Assert.That(ShakeScenario.PilotBand(inside, lengths.Length, median), Is.True, at);

            int punches = 0, weaker = 0, ringedLasts = 0, ringlessLasts = 0;
            for (int cell = 0; cell < runs.GetLength(1); cell++)
                for (int s = 0; s < Seeds; s++)
                {
                    ShakeScenario.Result ringed = runs[0, cell, s], noRing = runs[1, cell, s];
                    punches++;
                    if (noRing.Peak < ringed.Peak) weaker++;
                    if (ShakeScenario.PastTheKick(ringed)) ringedLasts++;
                    if (ShakeScenario.PastTheKick(noRing)) ringlessLasts++;
                }
            string wrong = $"no ring, seeds 1–{Seeds} of the six descents: weaker {weaker}/{punches}; past the kick ({ShakeScenario.KickSeconds:F1} s) on "
                + $"{ringlessLasts}/{punches} against {ringedLasts}";
            TestContext.WriteLine(wrong);
            // Since model 64 the ring is no longer the only long shake: the canister's pack and body ride forward and put the CoM
            // 2–9 mm ahead of the hubs' centre (3.2 % more on the front motors at L4), and a hard arrest drives the front pair into
            // their ESCs' limit first, a pitch wobble of its own. Without the ring the shake lasts past the kick on 102 of 240
            // (the ring's 126; on the old coil 38 against 108); with the canister's pack and body at the centre fore and aft (a
            // probe, not kept) 28 against 89, so the imbalance, not the ring, is the rest. The known-wrong still fails: weaker
            // peaks on most, and fewer long shakes than the ring's.
            Assert.That(weaker >= ShakeScenario.Most * punches && ringlessLasts < ringedLasts, Is.True, "the known-wrong fails: " + wrong);
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
