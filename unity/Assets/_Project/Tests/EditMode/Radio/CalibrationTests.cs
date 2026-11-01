using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.Input.Radio;
using Fpv450.Sim.Control;
using NUnit.Framework;

namespace Fpv450.Tests.Radio
{
    // tx-input (p6-input-app 1.2, INP-02): "The TX12 pitfall is caught" with fc-profiles' spans, "Under two minutes, no
    // keyboard" with a synthetic pilot on a synthetic TX12 (the by-hand run with a real TX12 is the pilot's, at G7),
    // the channel order, reversal, endpoints, deadband and arm switch, radio-side curves, the calibrated map, the store
    // and the reader's map and tap.
    public sealed class CalibrationTests
    {
        static CalibrationWizard Calibrate(Tx12Radio radio, CalibratingPilot pilot = null, long reportUs = 2000, long updateUs = 0)
        {
            CalibrationWizard w = Calibrations.Run(radio, pilot ?? new CalibratingPilot(radio), out _, 300000000, reportUs, updateUs);
            Assert.AreEqual(CalibrationStep.Done, w.Step, "the wizard finished");
            TestContext.WriteLine("flow " + (w.LastUs - w.FirstUs) / 1e6 + " s; order " + w.Profile.Order + "; findings: " +
                (w.Findings.Count == 0 ? "none" : string.Join(" | ", w.Findings.Select(f => f.Kind + " " + f.Message))));
            return w;
        }

        static double Mapped(CalibrationProfile p, Tx12Radio radio, Func<StickSample, double> axis)
        {
            var r = new RadioReport(Tx12Radio.Axes.Length);
            radio.Fill(5000, r);
            new CalibratedStickMap(p).Map(r, Tx12Radio.Axes, out StickSample s);
            return axis(s);
        }

        // ---- Under two minutes, no keyboard ----

        [Test]
        public void HealthyTx12CalibratesOnTheRadioAloneUnderTwoMinutes()
        {
            var radio = new Tx12Radio();
            CalibrationWizard w = Calibrate(radio);
            CalibrationProfile p = w.Profile;

            Assert.IsFalse(w.Failed);
            Assert.IsEmpty(w.Findings);
            Assert.Less(w.LastUs - w.FirstUs, 120000000, "under 2 minutes, with Feed (the radio) the only input");
            Assert.AreEqual("AETR", p.Order);
            Assert.AreEqual(new[] { 0, 1, 2, 3 }, new[] { p.Roll.Axis, p.Pitch.Axis, p.Throttle.Axis, p.Yaw.Axis });
            Assert.IsFalse(p.Roll.Reversed || p.Pitch.Reversed || p.Yaw.Reversed || p.Throttle.Reversed);
            foreach (StickCalibration c in new[] { p.Roll, p.Pitch, p.Yaw, p.Throttle })
            {
                Assert.LessOrEqual(c.Min, 2, "the endpoints are the radio's full travel");
                Assert.GreaterOrEqual(c.Max, 2045);
            }
            foreach (StickCalibration c in new[] { p.Roll, p.Pitch, p.Yaw })
            {
                Assert.That(c.Centre, Is.InRange(1020, 1028));
                Assert.That(c.Deadband, Is.InRange(1, 4), "the deadband is the rest noise");
            }
            Assert.AreEqual(ArmControl.Axis, p.Arm.Control);
            Assert.AreEqual(4, p.Arm.Index, "the arm switch on ch5");
            Assert.IsTrue(p.Arm.OnHigh);

            // The calibrated sticks: centre 0, the ends ±1, the throttle 0 to 1, the arm switch.
            Assert.AreEqual(0, Mapped(p, radio, s => s.Roll));
            radio.Stick[Tx12Radio.Roll] = 1;
            radio.Stick[Tx12Radio.Pitch] = -1;
            radio.Stick[Tx12Radio.Yaw] = 0.5;
            radio.Stick[Tx12Radio.Throttle] = 1;
            radio.Arm = true;
            Assert.AreEqual(1, Mapped(p, radio, s => s.Roll), 1e-3, "the ends ±1 within the radio's ±1 count of jitter");
            Assert.AreEqual(-1, Mapped(p, radio, s => s.Pitch), 1e-3);
            Assert.AreEqual(0.5, Mapped(p, radio, s => s.Yaw), 0.01);
            Assert.AreEqual(1, Mapped(p, radio, s => s.Throttle), 1e-3);
            Assert.AreEqual(1, Mapped(p, radio, s => s.Arm ? 1 : 0));
        }

        [Test]
        public void TaerReversalsAndAButtonArmAreDetected()
        {
            var radio = new Tx12Radio
            {
                Channel = new[] { 1, 2, 0, 3 }, // TAER
                Reverse = new[] { false, true, true, false }, // pitch and throttle reversed on the radio
                ArmChannel = -1,
                ArmButton = 3, // channel 12
            };
            CalibrationProfile p = Calibrate(radio).Profile;

            Assert.AreEqual("TAER", p.Order);
            Assert.IsTrue(p.Pitch.Reversed && p.Throttle.Reversed);
            Assert.IsFalse(p.Roll.Reversed || p.Yaw.Reversed);
            Assert.AreEqual(ArmControl.Button, p.Arm.Control);
            Assert.AreEqual(3, p.Arm.Index);
            radio.Stick[Tx12Radio.Pitch] = 1;
            radio.Stick[Tx12Radio.Throttle] = 0.25;
            Assert.AreEqual(1, Mapped(p, radio, s => s.Pitch), 1e-3, "forward is +1 whatever the radio's reversal");
            Assert.AreEqual(0.25, Mapped(p, radio, s => s.Throttle), 0.01);
            Assert.IsFalse(Mapped(p, radio, s => s.Arm ? 1 : 0) > 0);
        }

        [Test]
        public void AHastySweepIsRepeatedSlowerNotFlagged()
        {
            var radio = new Tx12Radio();
            var pilot = new CalibratingPilot(radio) { FirstSweepUs = 400000 };
            CalibrationWizard w = Calibrate(radio, pilot);

            Assert.IsTrue(pilot.SawTooFast, "the wizard asked for a slower sweep");
            Assert.IsEmpty(w.Findings);
            Assert.AreEqual("AETR", w.Profile.Order);
            Assert.Less(w.LastUs - w.FirstUs, 120000000);
        }

        // ---- The TX12 pitfall is caught ----

        // fc-profiles: the pilot's TX12 MKII on EdgeTX 2.8.0 had two axes calibrated with spans of 79 / 1 and 85 / 0,
        // against healthy spans of 683–903. EdgeTX divides by max(100, span), so those axes reach full output about 100
        // ADC counts from the centre, of their 683–903: in the reports they jump from end to end, like a switch.
        [Test]
        public void Tx12PitfallSpansAreNamedAsTooNarrow()
        {
            var radio = new Tx12Radio
            {
                TravelNeg = new[] { 811, 742, 683, 868 },
                TravelPos = new[] { 794, 903, 720, 851 },
                SpanNeg = new[] { 79, 85, 683, 868 },
                SpanPos = new[] { 1, 0, 720, 851 },
            };
            CalibrationWizard w = Calibrate(radio);

            Assert.IsTrue(w.Failed, "a profile with a switch-like axis is not saved");
            Assert.AreEqual(new[] { "Pitch", "Roll" }, w.Findings.Where(f => f.Kind == FindingKind.TooNarrow).Select(f => f.Stick).OrderBy(s => s).ToArray());
            Assert.IsTrue(w.Findings.All(f => f.Kind == FindingKind.TooNarrow), "the healthy spans (683–903) pass");
            foreach (CalibrationFinding f in w.Findings)
                StringAssert.Contains("Recalibrate the radio", f.Message);
            StringAssert.Contains("Recalibrate the radio", w.Prompt);
        }

        // QA's probe E9 (interim review, finding 2): on the pitfall's axes the reports cross 10–90 % in about a tenth of
        // the stick's sweep. A careful first sweep of 3 s is "too fast", and the repeat at 7.5 s crosses in 0.75 s, over
        // the jump rule's 0.7 s: both axes were saved as sticks. A pilot slow from the start is never even asked. The
        // same pilots on a healthy radio find nothing.
        [TestCase(3000000, 7500000, TestName = "E9: the pitfall swept in 3 s, then 7.5 s after Too fast, is too narrow")]
        [TestCase(8000000, 8000000, TestName = "E9: the pitfall swept in 8 s from the start is too narrow")]
        public void ASlowPilotStillFindsTheTx12Pitfall(long firstSweepUs, long sweepUs)
        {
            var radio = new Tx12Radio
            {
                SpanNeg = new[] { 79, 85, 683, 868 },
                SpanPos = new[] { 1, 0, 720, 851 },
            };
            CalibrationWizard w = Calibrate(radio, new CalibratingPilot(radio) { FirstSweepUs = firstSweepUs, SweepUs = sweepUs });

            Assert.IsTrue(w.Failed, "nothing is saved");
            Assert.AreEqual(new[] { "Pitch", "Roll" }, w.Findings.Where(f => f.Kind == FindingKind.TooNarrow).Select(f => f.Stick).OrderBy(s => s).ToArray());
            Assert.IsTrue(w.Findings.All(f => f.Kind == FindingKind.TooNarrow), "the healthy throttle and yaw pass");
            foreach (CalibrationFinding f in w.Findings)
                StringAssert.Contains("Recalibrate the radio", f.Message);

            var healthy = new Tx12Radio();
            CalibrationWizard h = Calibrate(healthy, new CalibratingPilot(healthy) { FirstSweepUs = firstSweepUs, SweepUs = sweepUs });
            Assert.IsFalse(h.Failed);
            Assert.IsEmpty(h.Findings, "the same pilot on a healthy radio");
        }

        // QA's probe E9v (interim review, finding 9 and gap 2a). The rest spread rule refused a healthy radio whose rest
        // spread 8–10 HID counts (±4–5 ADC counts of jitter, or a thumb's ±0.4 % at the centre step), and passed a slow
        // pilot on a pitfall whose ADC is still at rest (EdgeTX 2.8's jitter filter). The smallest step rule catches the
        // pitfall at every sweep and jitter, and saves every healthy radio. The repeat after "Too fast" is 2.5 times
        // slower, as QA's pilot's.
        static readonly int[] PitfallNeg = { 79, 85, 683, 868 }, PitfallPos = { 1, 0, 720, 851 };

        static IEnumerable<TestCaseData> PitfallCases()
        {
            foreach ((int lo, int hi) in new[] { (0, 0), (0, 1), (-1, 1), (-1, 2), (-2, 2) })
                foreach (long sweep in new long[] { 2000000, 3000000, 8000000, 12000000 })
                    yield return new TestCaseData(lo, hi, sweep).SetName("E9v: the pitfall, jitter " + lo + " to " + hi + ", sweep " + sweep / 1000000 + " s, is too narrow");
        }

        static CalibratingPilot Sweeping(Tx12Radio radio, long sweepUs) =>
            new CalibratingPilot(radio) { FirstSweepUs = sweepUs, SweepUs = sweepUs * 5 / 2 };

        static string[] TooNarrow(CalibrationWizard w) =>
            w.Findings.Where(f => f.Kind == FindingKind.TooNarrow).Select(f => f.Stick).OrderBy(s => s).ToArray();

        [TestCaseSource(nameof(PitfallCases))]
        public void ThePitfallIsTooNarrowAtAnySweepAndJitter(int noiseLo, int noiseHi, long sweepUs)
        {
            var radio = new Tx12Radio { SpanNeg = PitfallNeg, SpanPos = PitfallPos, NoiseLo = noiseLo, NoiseHi = noiseHi };
            CalibrationWizard w = Calibrate(radio, Sweeping(radio, sweepUs));

            Assert.IsTrue(w.Failed, "nothing is saved");
            Assert.AreEqual(new[] { "Pitch", "Roll" }, TooNarrow(w));
            Assert.IsTrue(w.Findings.All(f => f.Kind == FindingKind.TooNarrow), "the healthy throttle and yaw pass");
            foreach (CalibrationFinding f in w.Findings)
                StringAssert.Contains("Recalibrate the radio", f.Message);
        }

        static IEnumerable<TestCaseData> HealthyCases()
        {
            foreach (int span in new[] { 683, 800, 903 })
                foreach (int j in new[] { 1, 2, 3, 4, 5 })
                    foreach (long sweep in new long[] { 3000000, 8000000 })
                        yield return new TestCaseData(span, j, sweep).SetName("E9v: a healthy span " + span + ", jitter ±" + j + ", sweep " + sweep / 1000000 + " s, is saved");
        }

        [TestCaseSource(nameof(HealthyCases))]
        public void AHealthyRadioIsSavedAtAnyJitter(int span, int jitter, long sweepUs)
        {
            var radio = new Tx12Radio
            {
                TravelNeg = new[] { span, span, 683, span },
                TravelPos = new[] { span, span, 720, span },
                NoiseLo = -jitter,
                NoiseHi = jitter,
            };
            CalibrationWizard w = Calibrate(radio, Sweeping(radio, sweepUs));

            Assert.IsFalse(w.Failed);
            Assert.IsEmpty(w.Findings);
        }

        // QA's finding 10 (interim review, probes E9w F and E9x): the smallest step rule assumed a report every 2 ms. A
        // radio that updates every 3 or 4 ms, or a 1 ms stream repeating a 4 ms output, moves 5–9 counts an update on a
        // quick healthy sweep, and was refused as the pitfall. Every healthy sweep is now saved or asked to go slower,
        // and the pitfall is still named once its sweep is slow enough. The pilot goes 2.5 times slower after each
        // "Too fast", as QA's.
        static readonly long[][] SlowRadios = { new long[] { 3000, 0 }, new long[] { 4000, 0 }, new long[] { 1000, 4000 } };

        static string Radio(long[] r) => r[1] == 0 ? "every " + r[0] / 1000 + " ms" : r[0] / 1000 + " ms over " + r[1] / 1000 + " ms";

        static IEnumerable<TestCaseData> SlowHealthyCases()
        {
            foreach (long[] r in SlowRadios)
                foreach (int span in new[] { 683, 800, 903 })
                    foreach (int j in new[] { 0, 1 })
                        foreach (long sweep in new long[] { 900000, 1000000, 1100000, 1200000, 1500000, 2000000, 3000000, 8000000 })
                            yield return new TestCaseData(r[0], r[1], span, j, sweep).SetName("E10: a healthy span " + span + " " + Radio(r) + ", jitter ±" + j + ", sweep " + sweep / 1000 + " ms, is saved");
        }

        [TestCaseSource(nameof(SlowHealthyCases))]
        public void AHealthyRadioSlowerThan2MsIsSavedOrAskedSlower(long reportUs, long updateUs, int span, int jitter, long sweepUs)
        {
            var radio = new Tx12Radio
            {
                TravelNeg = new[] { span, span, 683, span },
                TravelPos = new[] { span, span, 720, span },
                NoiseLo = -jitter,
                NoiseHi = jitter,
            };
            CalibratingPilot pilot = Sweeping(radio, sweepUs);
            CalibrationWizard w = Calibrate(radio, pilot, reportUs, updateUs);
            TestContext.WriteLine("asked slower " + pilot.SawTooFast + "; update interval " + w.UpdateUs + " us");

            Assert.IsFalse(w.Failed, "never refused");
            Assert.IsEmpty(w.Findings);
            long update = updateUs > 0 ? updateUs : reportUs;
            Assert.That(w.UpdateUs, Is.InRange(update - 40, update + 40), "the radio's update interval, from its changes, not its reports");
        }

        static IEnumerable<TestCaseData> SlowPitfallCases()
        {
            foreach (long[] r in SlowRadios)
                foreach (int j in new[] { 0, 1 })
                    foreach (long sweep in new long[] { 2000000, 3000000, 8000000, 12000000 })
                        yield return new TestCaseData(r[0], r[1], j, sweep).SetName("E10: the pitfall " + Radio(r) + ", jitter ±" + j + ", sweep " + sweep / 1000000 + " s, is too narrow");
        }

        [TestCaseSource(nameof(SlowPitfallCases))]
        public void ThePitfallSlowerThan2MsIsTooNarrowOnceSweptSlowEnough(long reportUs, long updateUs, int jitter, long sweepUs)
        {
            var radio = new Tx12Radio { SpanNeg = PitfallNeg, SpanPos = PitfallPos, NoiseLo = -jitter, NoiseHi = jitter };
            CalibratingPilot pilot = Sweeping(radio, sweepUs);
            CalibrationWizard w = Calibrate(radio, pilot, reportUs, updateUs);
            TestContext.WriteLine("asked slower " + pilot.SawTooFast + "; update interval " + w.UpdateUs + " us");

            Assert.IsTrue(w.Failed, "nothing is saved");
            Assert.AreEqual(new[] { "Pitch", "Roll" }, TooNarrow(w));
            Assert.IsTrue(w.Findings.All(f => f.Kind == FindingKind.TooNarrow), "the healthy throttle and yaw pass");
        }

        [TestCase(0.002, TestName = "E9v: a thumb's ±0.2 % at the centre step is saved")]
        [TestCase(0.004, TestName = "E9v: a thumb's ±0.4 % at the centre step is saved")]
        [TestCase(0.006, TestName = "E9v: a thumb's ±0.6 % at the centre step is saved")]
        [TestCase(0.01, TestName = "E9v: a thumb's ±1 % at the centre step is saved")]
        [TestCase(0.015, TestName = "E9v: a thumb's ±1.5 % at the centre step is saved")]
        public void AThumbLeftOnTheSticksAtTheCentreStepIsNotASwitch(double wobble)
        {
            var radio = new Tx12Radio { TravelNeg = new[] { 800, 800, 683, 800 }, TravelPos = new[] { 800, 800, 720, 800 } };
            CalibrationWizard w = Calibrate(radio, new CalibratingPilot(radio) { Wobble = wobble });

            Assert.IsFalse(w.Failed);
            Assert.IsEmpty(w.Findings);
        }

        // The throttle rests at its end, where EdgeTX clamps, so its rest says nothing; its sweep's smallest step does. And
        // a pitfall on one side only (EdgeTX scales each side by its own span): the coarse side is named.
        [TestCase(0, 0, 2000, 0, TestName = "E9v: a pitfall throttle and a one-sided pitfall roll, ADC still, are too narrow")]
        [TestCase(-1, 1, 2000, 0, TestName = "E9v: a pitfall throttle and a one-sided pitfall roll, jitter ±1, are too narrow")]
        [TestCase(0, 0, 3000, 0, TestName = "E10: a pitfall throttle and a one-sided pitfall roll, ADC still, every 3 ms, are too narrow")]
        [TestCase(0, 0, 4000, 0, TestName = "E10: a pitfall throttle and a one-sided pitfall roll, ADC still, every 4 ms, are too narrow")]
        [TestCase(-1, 1, 1000, 4000, TestName = "E10: a pitfall throttle and a one-sided pitfall roll, jitter ±1, 1 ms over 4 ms, are too narrow")]
        public void APitfallThrottleAndAOneSidedPitfallAreTooNarrow(int noiseLo, int noiseHi, long reportUs, long updateUs)
        {
            var radio = new Tx12Radio
            {
                SpanNeg = new[] { 79, 742, 79, 868 },
                SpanPos = new[] { 794, 903, 1, 851 },
                NoiseLo = noiseLo,
                NoiseHi = noiseHi,
            };
            CalibrationWizard w = Calibrate(radio, Sweeping(radio, 8000000), reportUs, updateUs);

            Assert.IsTrue(w.Failed, "nothing is saved");
            Assert.AreEqual(new[] { "Roll", "Throttle" }, TooNarrow(w));
            Assert.IsTrue(w.Findings.All(f => f.Kind == FindingKind.TooNarrow), "the healthy pitch and yaw pass");
        }

        // QA's finding 11 (interim review, probes E10q and E10r): the update guard put the pitfall off for as long as the
        // pilot did not slow down, so a one-sided pitfall swept at the prompt's own 2 s heard "Still too fast" forever.
        // The prompt now asks how many times slower the coarse side needs, and a repeat no slower names the axis. A literal
        // pilot sweeps the same every time; a stubborn one goes 2.5 times slower once, as QA's.
        static readonly long[][] AllRadios = { new long[] { 2000, 0 }, new long[] { 3000, 0 }, new long[] { 4000, 0 }, new long[] { 1000, 4000 } };

        static CalibratingPilot Literal(Tx12Radio radio, long sweepUs) =>
            new CalibratingPilot(radio) { FirstSweepUs = sweepUs, SweepUs = sweepUs, Slowing = 1 };

        static CalibratingPilot Stubborn(Tx12Radio radio, long sweepUs) =>
            new CalibratingPilot(radio) { FirstSweepUs = sweepUs, SweepUs = sweepUs * 5 / 2, Slowing = 1 };

        static Tx12Radio OneSided(int jitter) =>
            new Tx12Radio { SpanNeg = new[] { 79, 742, 79, 868 }, SpanPos = new[] { 794, 903, 1, 851 }, NoiseLo = -jitter, NoiseHi = jitter };

        static IEnumerable<TestCaseData> LiteralPitfallCases()
        {
            foreach (long[] r in AllRadios)
                foreach (long sweep in new long[] { 2000000, 2500000, 3000000 })
                    yield return new TestCaseData(r[0], r[1], sweep).SetName("E10: a one-sided pitfall roll " + Radio(r) + ", swept " + sweep / 1000 + " ms every time, is named");
        }

        [TestCaseSource(nameof(LiteralPitfallCases))]
        public void AOneSidedPitfallSweptTheSameEveryTimeIsNamed(long reportUs, long updateUs, long sweepUs)
        {
            Tx12Radio radio = OneSided(1);
            CalibratingPilot pilot = Literal(radio, sweepUs);
            CalibrationWizard w = Calibrate(radio, pilot, reportUs, updateUs);
            TestContext.WriteLine("asked: " + string.Join(" | ", pilot.Prompts.Where(p => p.Contains("fast"))));

            Assert.IsTrue(w.Failed, "nothing is saved");
            Assert.AreEqual(new[] { "Roll", "Throttle" }, TooNarrow(w));
            Assert.Less(w.LastUs - w.FirstUs, 120000000, "under two minutes");
            Assert.IsTrue(pilot.Prompts.Any(p => p.StartsWith("Too fast. Roll") && p.Contains("times slower than that")), "asked how much slower its coarse side needs");
            foreach (CalibrationFinding f in w.Findings)
                StringAssert.Contains("Recalibrate the radio's sticks, then calibrate here again", f.Message, "the pitfall's steps are 10 or more: the radio is named first");
            Assert.AreEqual("Recalibrate the radio's sticks, then calibrate here again.", w.Prompt);
        }

        static IEnumerable<TestCaseData> StubbornPitfallCases()
        {
            foreach (long[] r in AllRadios)
                foreach (bool oneSided in new[] { false, true })
                    foreach (int j in new[] { 0, 1 })
                        foreach (long sweep in new long[] { 2000000, 3000000, 8000000, 12000000 })
                            yield return new TestCaseData(r[0], r[1], oneSided, j, sweep).SetName("E10: the " + (oneSided ? "one-sided pitfall roll and throttle" : "pitfall") + " " + Radio(r) + ", jitter ±" + j + ", swept " + sweep / 1000000 + " s by a stubborn pilot, is named");
        }

        [TestCaseSource(nameof(StubbornPitfallCases))]
        public void ThePitfallSweptByAStubbornPilotIsNamed(long reportUs, long updateUs, bool oneSided, int jitter, long sweepUs)
        {
            Tx12Radio radio = oneSided ? OneSided(jitter) : new Tx12Radio { SpanNeg = PitfallNeg, SpanPos = PitfallPos, NoiseLo = -jitter, NoiseHi = jitter };
            CalibrationWizard w = Calibrate(radio, Stubborn(radio, sweepUs), reportUs, updateUs);

            Assert.IsTrue(w.Failed, "nothing is saved");
            Assert.AreEqual(oneSided ? new[] { "Roll", "Throttle" } : new[] { "Pitch", "Roll" }, TooNarrow(w));
            Assert.IsTrue(w.Findings.All(f => f.Kind == FindingKind.TooNarrow), "the healthy sticks pass");
        }

        static IEnumerable<TestCaseData> LiteralHealthyCases()
        {
            foreach (long[] r in AllRadios)
                foreach (int span in new[] { 683, 800, 903 })
                    foreach (long sweep in new long[] { 2000000, 2500000, 3000000 })
                        yield return new TestCaseData(r[0], r[1], span, sweep).SetName("E10: a healthy span " + span + " " + Radio(r) + ", swept " + sweep / 1000 + " ms every time, is saved");
        }

        [TestCaseSource(nameof(LiteralHealthyCases))]
        public void AHealthyRadioSweptTheSameEveryTimeIsSaved(long reportUs, long updateUs, int span, long sweepUs)
        {
            var radio = new Tx12Radio { TravelNeg = new[] { span, span, 683, span }, TravelPos = new[] { span, span, 720, span } };
            CalibrationWizard w = Calibrate(radio, Literal(radio, sweepUs), reportUs, updateUs);

            Assert.IsFalse(w.Failed, "never refused");
            Assert.IsEmpty(w.Findings);
        }

        static IEnumerable<TestCaseData> StubbornHealthyCases()
        {
            foreach (long[] r in AllRadios)
                foreach (int span in new[] { 683, 800, 903 })
                    foreach (long sweep in new long[] { 900000, 1000000, 1200000, 1500000 })
                        yield return new TestCaseData(r[0], r[1], span, sweep).SetName("E10: a healthy span " + span + " " + Radio(r) + ", swept " + sweep / 1000 + " ms by a stubborn pilot, is saved");
        }

        [TestCaseSource(nameof(StubbornHealthyCases))]
        public void AHealthyRadioSweptByAStubbornPilotIsSaved(long reportUs, long updateUs, int span, long sweepUs)
        {
            var radio = new Tx12Radio { TravelNeg = new[] { span, span, 683, span }, TravelPos = new[] { span, span, 720, span } };
            CalibrationWizard w = Calibrate(radio, Stubborn(radio, sweepUs), reportUs, updateUs);

            Assert.IsFalse(w.Failed, "never refused");
            Assert.IsEmpty(w.Findings);
        }

        // QA's finding 12 (interim review, probe E12u): EdgeTX runs its mixer, and so its USB output, at the RF module's
        // period (ELRS at 150 Hz: 6.7 ms; at 50 Hz: 20 ms). UpdateUs's 4 ms bound read such a radio as faster than it was,
        // and refused its healthy sweeps as switches, at 20 ms every one. A pilot who follows the prompt is saved.
        [TestCase(7000, 1, TestName = "E12: a healthy radio, 1 ms over 7 ms, jitter ±1, swept 2 s by a pilot who follows the prompt, is saved")]
        [TestCase(9000, 1, TestName = "E12: a healthy radio, 1 ms over 9 ms, jitter ±1, swept 2 s by a pilot who follows the prompt, is saved")]
        [TestCase(20000, 1, TestName = "E12: a healthy radio, 1 ms over 20 ms, jitter ±1, swept 2 s by a pilot who follows the prompt, is saved")]
        [TestCase(20000, 0, TestName = "E12: a healthy radio, 1 ms over 20 ms, ADC still, swept 2 s by a pilot who follows the prompt, is saved")]
        public void AHealthyRadioSlowerThan4MsIsSavedWhenThePromptIsFollowed(long updateUs, int jitter)
        {
            var radio = new Tx12Radio { NoiseLo = -jitter, NoiseHi = jitter };
            var pilot = new CalibratingPilot(radio) { FollowsPrompt = true };
            CalibrationWizard w = Calibrate(radio, pilot, 1000, updateUs);
            TestContext.WriteLine("asked: " + string.Join(" | ", pilot.Prompts.Where(p => p.Contains("fast"))) + "; update interval " + w.UpdateUs + " us");

            Assert.IsFalse(w.Failed, "never refused");
            Assert.IsEmpty(w.Findings);
            Assert.That(w.UpdateUs, Is.InRange(updateUs - 40, updateUs + 40), "the radio's update interval, slower than 4 ms");
            Assert.Less(w.LastUs - w.FirstUs, 120000000, "under two minutes");
        }

        // QA's nit on finding 11's refusal: a healthy radio swept quicker than the prompt, then no slower, is refused (QA's
        // E11t trade-off), but its step is under EdgeTX's floor gain of 10, so the refusal leads with the speed, never the
        // radio, and asks how much slower.
        static IEnumerable<TestCaseData> QuickLiteralHealthyCases()
        {
            foreach (long[] r in AllRadios)
                foreach (int span in new[] { 683, 800, 903 })
                    foreach (long sweep in new long[] { 900000, 1000000, 1100000, 1200000, 1500000 })
                        yield return new TestCaseData(r[0], r[1], span, sweep).SetName("E12: a healthy span " + span + " " + Radio(r) + ", swept " + sweep / 1000 + " ms every time, is saved or told to sweep slower");
        }

        [TestCaseSource(nameof(QuickLiteralHealthyCases))]
        public void AHealthyRadioSweptQuicklyEveryTimeIsNeverToldToRecalibrateTheRadioFirst(long reportUs, long updateUs, int span, long sweepUs)
        {
            var radio = new Tx12Radio { TravelNeg = new[] { span, span, 683, span }, TravelPos = new[] { span, span, 720, span } };
            CalibrationWizard w = Calibrate(radio, Literal(radio, sweepUs), reportUs, updateUs);

            foreach (CalibrationFinding f in w.Findings)
                StringAssert.IsMatch(@"^\w+ was not swept slower, so it cannot be told from a switch\. Calibrate here again, sweeping it about ([2-9]|10) times slower\. If it is refused again, recalibrate the radio's sticks\.$", f.Message);
            Assert.AreEqual(w.Failed ? "Calibrate here again, sweeping slower." : "Calibrated.", w.Prompt);
        }

        [Test]
        public void AHealthyRadioSweptIn1SecondEveryTimeIsToldToSweepSlower()
        {
            var radio = new Tx12Radio { TravelNeg = new[] { 800, 800, 683, 800 }, TravelPos = new[] { 800, 800, 720, 800 } };
            CalibrationWizard w = Calibrate(radio, Literal(radio, 1000000), 4000);

            Assert.IsTrue(w.Failed, "refused: swept quicker than the prompt, then no slower");
            Assert.IsNotEmpty(w.Findings);
            foreach (CalibrationFinding f in w.Findings)
                StringAssert.StartsWith(f.Stick + " was not swept slower, so it cannot be told from a switch. Calibrate here again, sweeping it about", f.Message);
            Assert.AreEqual("Calibrate here again, sweeping slower.", w.Prompt);
        }

        // QA's nit: the factor asked is capped, so a side whose in-band reports share one stamp (held 0) asks 10.
        [TestCase(0.0, 10)]
        [TestCase(0.01, 10)]
        [TestCase(0.15, 7)]
        [TestCase(0.3, 4)]
        [TestCase(0.6, 2)]
        [TestCase(0.99, 2)]
        public void TheFactorAskedIs2To10(double held, int times) => Assert.AreEqual(times, CalibrationWizard.TimesSlower(held));

        // QA's pre-existing gap: a pitfall with ±3 ADC counts of rest jitter spreads 61 HID counts, over the centre step's
        // 2 % still band, so the step never settles. After 5 s the prompt names the channel.
        [Test]
        public void APitfallTooJitteryToSettleIsNamedAtTheCentreStep()
        {
            var radio = new Tx12Radio { SpanNeg = PitfallNeg, SpanPos = PitfallPos, NoiseLo = -3, NoiseHi = 3 };
            CalibrationWizard early = Calibrations.Run(radio, new CalibratingPilot(radio), out _, 1000000 + 4900000);
            Assert.AreEqual(CalibrationStep.Centre, early.Step);
            Assert.AreEqual(-1, early.Restless, "not before 5 s");
            StringAssert.DoesNotContain("keeps moving", early.Prompt);

            CalibrationWizard w = Calibrations.Run(radio, new CalibratingPilot(radio), out _, 1000000 + 10000000);
            TestContext.WriteLine(w.Prompt);
            Assert.AreEqual(CalibrationStep.Centre, w.Step, "the centre step never settles");
            Assert.That(w.Restless, Is.InRange(0, 1), "roll's or pitch's channel");
            StringAssert.Contains("Channel " + (w.Restless + 1) + " keeps moving", w.Prompt);
            StringAssert.Contains("recalibrate the radio's sticks", w.Prompt);
        }

        // The same spans read as the reports' own range (design 4's rule: under 200 of the 2048).
        [Test]
        public void AxesSpanningOnly79And85CountsAreNamedAsTooNarrow()
        {
            var radio = new Tx12Radio { HidSpan = new[] { 79, 85, 0, 0 } };
            CalibrationWizard w = Calibrate(radio);

            Assert.IsTrue(w.Failed);
            CalibrationFinding[] narrow = w.Findings.Where(f => f.Kind == FindingKind.TooNarrow).OrderBy(f => f.Stick).ToArray();
            Assert.AreEqual(new[] { "Pitch", "Roll" }, narrow.Select(f => f.Stick).ToArray());
            Assert.That(Spans(narrow[0].Message), Is.InRange(85, 87), "pitch: 85 and the ±1 jitter");
            Assert.That(Spans(narrow[1].Message), Is.InRange(79, 81), "roll: 79 and the ±1 jitter");
            StringAssert.Contains("of 2048: it behaves like a switch", narrow[0].Message);
            Assert.AreEqual(2, w.Findings.Count, "the healthy throttle and yaw pass");
        }

        static int Spans(string message) =>
            int.Parse(message.Substring(message.IndexOf("spans only ", StringComparison.Ordinal) + 11).Split(' ')[0]);

        // ---- Radio-side curves ----

        [Test]
        public void RadioExpoAndDualRatesAreNamed()
        {
            var radio = new Tx12Radio
            {
                Expo = new[] { 0, 0, 0, 30 },    // yaw expo 30 % on the radio
                Weight = new[] { 70, 100, 100, 100 }, // roll at 70 % (a dual rate)
            };
            CalibrationWizard w = Calibrate(radio);

            Assert.IsFalse(w.Failed, "curves are named, not blocking");
            Assert.AreEqual(2, w.Findings.Count);
            CalibrationFinding curve = w.Findings.Single(f => f.Kind == FindingKind.RadioCurve);
            CalibrationFinding shortEnd = w.Findings.Single(f => f.Kind == FindingKind.ShortEndpoint);
            Assert.AreEqual("Yaw", curve.Stick);
            StringAssert.Contains("switch off expo", curve.Message);
            Assert.AreEqual("Roll", shortEnd.Stick);
            StringAssert.Contains("70 %", shortEnd.Message);
            StringAssert.Contains("dual rates", shortEnd.Message);
        }

        // ---- The calibrated map ----

        [Test]
        public void CalibratedMapIsPiecewiseAboutTheCentreWithTheDeadband()
        {
            var p = new CalibrationProfile();
            p.Roll = new StickCalibration { Axis = 0, Min = 10, Centre = 1030, Max = 2040, Deadband = 3 };
            p.Pitch = new StickCalibration { Axis = 1, Min = 10, Centre = 1030, Max = 2040, Reversed = true };
            p.Yaw = new StickCalibration { Axis = 7 }; // the device lacks it
            p.Throttle = new StickCalibration { Axis = 2, Min = 100, Max = 1900 };
            p.Arm = new ArmCalibration { Control = ArmControl.Axis, Index = 3, Threshold = 1000, OnHigh = false };
            var map = new CalibratedStickMap(p);
            p.Roll.Centre = 0; // the map keeps its own copy
            var axes = new RadioAxis[4];
            var r = new RadioReport(4);

            StickSample s = default;
            void At(int roll, int pitch, int thr, int arm)
            {
                r.Axes[0] = roll;
                r.Axes[1] = pitch;
                r.Axes[2] = thr;
                r.Axes[3] = arm;
                map.Map(r, axes, out s);
            }

            At(1033, 1030, 100, 2000);
            Assert.AreEqual(0, s.Roll, "inside the deadband");
            Assert.AreEqual(0, s.Pitch);
            Assert.AreEqual(0, s.Yaw);
            Assert.AreEqual(0, s.Throttle);
            Assert.IsFalse(s.Arm);
            At(1034, 2040, 1000, 500);
            Assert.AreEqual(1.0 / (2040 - 1030 - 3), s.Roll, 1e-12, "no step at the deadband's edge");
            Assert.AreEqual(-1, s.Pitch, 1e-12, "reversed");
            Assert.AreEqual(0.5, s.Throttle, 1e-12);
            Assert.IsTrue(s.Arm, "on below the threshold");
            At(2040, 10, 1900, 500);
            Assert.AreEqual(1, s.Roll, 1e-12);
            Assert.AreEqual(1, s.Pitch, 1e-12);
            Assert.AreEqual(1, s.Throttle, 1e-12);
            At(0, 0, 2047, 500);
            Assert.AreEqual(-1, s.Roll, 1e-12, "clamped past the endpoint");
            Assert.AreEqual(1, s.Throttle, 1e-12);
            At(10 + (1030 - 3 - 10) / 2, 1030, 0, 500);
            Assert.AreEqual(-0.5, s.Roll, 1e-3, "the low side scales to its own endpoint");
        }

        // ---- Named profiles ----

        [Test]
        public void NamedProfilesRoundTripThroughTheStore()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fpv450-calibration-" + Guid.NewGuid().ToString("N"));
            try
            {
                CalibrationProfile tx12 = Calibrate(new Tx12Radio()).Profile;
                tx12.Name = "TX12 \"mode 2\"";
                CalibrationProfile taer = Calibrate(new Tx12Radio { Channel = new[] { 1, 2, 0, 3 }, ArmChannel = -1, ArmButton = 0 }).Profile;
                taer.Name = "Spare";

                var store = new CalibrationStore(dir);
                store.Put(tx12);
                store.Put(taer);
                store.Put(tx12); // replaced by name, and active again
                store.Save();
                TestContext.WriteLine(File.ReadAllText(store.FilePath));

                CalibrationStore back = CalibrationStore.Load(dir);
                Assert.AreEqual(new[] { "Spare", "TX12 \"mode 2\"" }, back.Profiles.Select(p => p.Name).ToArray());
                Assert.AreEqual(tx12.Name, back.Active);
                Assert.AreEqual(back.ToJson(), store.ToJson(), "the file reads back field for field");
                CalibrationProfile a = back.ActiveProfile;
                Assert.AreEqual(tx12.Device, a.Device);
                Assert.AreEqual(tx12.Roll.Centre, a.Roll.Centre);
                Assert.AreEqual(tx12.Yaw.Deadband, a.Yaw.Deadband);
                Assert.AreEqual(ArmControl.Button, back.Find("Spare").Arm.Control);
                Assert.AreEqual("TAER", back.Find("Spare").Order);
                StringAssert.Contains("\"order\": \"AETR\"", File.ReadAllText(store.FilePath));
                Assert.IsEmpty(CalibrationStore.Load(Path.Combine(dir, "none")).Profiles, "no file yet: no profiles");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        // ---- The reader with the calibration ----

        [Test]
        public void ReaderMapsThroughTheCalibrationAndTapsEveryReport()
        {
            RecordedRadio radio = Tx12Stream.Make(0, 200000);
            radio.Clock = new VirtualClock { Now = long.MaxValue / 2 }; // every report is due: Read never waits
            CalibrationProfile profile = Calibrate(new Tx12Radio()).Profile;
            var map = new CalibratedStickMap(profile);
            var inbox = new StickInbox();
            var tap = new ReportTap(radio.Axes.Length);
            var reader = new RadioReader(Flights.Once(radio)) { Inbox = inbox, Map = map, Tap = tap };
            while (reader.Pump(0))
            {
            }

            var report = new RadioReport(radio.Axes.Length);
            for (int i = 0; i < radio.Count; i++)
            {
                Assert.IsTrue(tap.TryRead(report), "the tap has report " + i);
                Assert.AreEqual(radio.TimeOf(i), report.HostUs);
                map.Map(report, radio.Axes, out StickSample expected);
                inbox.Consume(radio.TimeOf(i), out StickSample got);
                Assert.AreEqual(expected, got, "report " + i);
            }
            Assert.IsFalse(tap.TryRead(report));
            Assert.AreEqual(0, tap.Dropped);
        }
    }
}
