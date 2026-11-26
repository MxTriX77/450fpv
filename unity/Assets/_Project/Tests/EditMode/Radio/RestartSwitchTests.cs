using System;
using System.IO;
using Fpv450.Input.Radio;
using NUnit.Framework;

namespace Fpv450.Tests.Radio
{
    // The restart switch (the pilot, 2026-11-25: the restart "is also on the Tx controller - A button"): a flip restarts
    // once, a held switch never repeats, a bounce is no flip; it is chosen on the radio in Calibrate and never takes the
    // arm switch or a stick; a saved profile reloads it, and a profile saved before it has none.
    public sealed class RestartSwitchTests
    {
        const long ReportUs = 2000;
        const int Low = 0, High = 2047;

        // The pilot's TX12 as their saved profile has it: AETR on channels 1–4, the arm on channel 5 (high).
        static CalibrationProfile Tx12Profile()
        {
            var p = new CalibrationProfile { Name = "TX12", Device = "Radiomaster TX12 Joystick" };
            foreach (var (c, axis) in new[] { (p.Roll, 0), (p.Pitch, 1), (p.Throttle, 2), (p.Yaw, 3) })
            {
                c.Axis = axis;
                c.Min = 0;
                c.Centre = 1024;
                c.Max = 2047;
            }
            p.Arm = new ArmCalibration { Control = ArmControl.Axis, Index = 4, Threshold = 1023, OnHigh = true };
            return p;
        }

        // A TX12 report: sticks centred, the throttle down, channels 5–8 as given, the buttons (channels 9–32).
        static RadioReport Report(long t, int ch5 = Low, int ch6 = Low, ulong buttons = 0, int throttle = Low)
        {
            var r = new RadioReport(8) { HostUs = t, Buttons = buttons };
            r.Axes[0] = r.Axes[1] = r.Axes[3] = 1024;
            r.Axes[2] = throttle;
            r.Axes[4] = ch5;
            r.Axes[5] = ch6;
            r.Axes[6] = 1024;
            r.Axes[7] = High;
            return r;
        }

        static readonly ArmCalibration Ch6 = new ArmCalibration { Control = ArmControl.Axis, Index = 5, Threshold = 1023, OnHigh = true };

        // The flips over a stretch of reports every 2 ms with the switch held on or off, from t; t moves on.
        static int Hold(SwitchFlip flip, ArmCalibration binding, bool on, long us, ref long t)
        {
            int flips = 0;
            for (long end = t + us; t < end; t += ReportUs)
                if (flip.Feed(Report(t, ch6: on ? High : Low), binding)) flips++;
            return flips;
        }

        [Test]
        public void AFlipRestartsOnceAndAHeldSwitchNeverRepeats()
        {
            var flip = new SwitchFlip();
            long t = 1000000;
            Assert.AreEqual(0, Hold(flip, Ch6, false, 1000000, ref t), "off: nothing");
            Assert.IsTrue(flip.Feed(Report(t, ch6: High), Ch6), "the flip on restarts at its first report");
            t += ReportUs;
            Assert.AreEqual(0, Hold(flip, Ch6, true, 5000000, ref t), "held on for 5 s: never again");
            Assert.AreEqual(0, Hold(flip, Ch6, false, 300000, ref t), "flipped off: nothing");
            Assert.AreEqual(1, Hold(flip, Ch6, true, 1000000, ref t), "flipped on again: once more");
        }

        [Test]
        public void ABounceIsNoFlip()
        {
            var flip = new SwitchFlip();
            long t = 1000000;
            Hold(flip, Ch6, false, 500000, ref t);
            Assert.AreEqual(1, Hold(flip, Ch6, true, 20000, ref t));
            Assert.AreEqual(0, Hold(flip, Ch6, false, SwitchFlip.OffUs - 2 * ReportUs, ref t), "off for under OffUs");
            Assert.AreEqual(0, Hold(flip, Ch6, true, 20000, ref t), "a contact's bounce: no second restart");
            Assert.AreEqual(0, Hold(flip, Ch6, false, SwitchFlip.OffUs + ReportUs, ref t));
            Assert.AreEqual(1, Hold(flip, Ch6, true, 20000, ref t), "off for OffUs or more: a flip");
        }

        [Test]
        public void ASwitchAlreadyOnOrNoneBoundNeverRestarts()
        {
            var flip = new SwitchFlip();
            long t = 1000000;
            Assert.AreEqual(0, Hold(flip, Ch6, true, 2000000, ref t), "on when first seen: no flip");
            flip.Reset();
            Assert.AreEqual(0, Hold(flip, Ch6, true, 100000, ref t), "on after the radio came back: no flip");
            var none = new ArmCalibration();
            for (int i = 0; i < 4; i++) Assert.AreEqual(0, Hold(flip, none, i % 2 == 1, 500000, ref t), "no switch bound");
            Assert.AreEqual(0, Hold(flip, null, true, 500000, ref t));
        }

        [Test]
        public void AButtonSwitchFlipsToo()
        {
            var sw = new ArmCalibration { Control = ArmControl.Button, Index = 2, OnHigh = true }; // channel 11
            var flip = new SwitchFlip();
            int flips = 0;
            for (long t = 0; t < 3000000; t += ReportUs)
                if (flip.Feed(Report(t, buttons: t >= 1000000 && t < 2000000 ? 1UL << 2 | 1UL : 1UL), sw)) flips++;
            Assert.AreEqual(1, flips, "on at 1 s, held a second; button 1 always on is not it");
        }

        [Test]
        public void TheSwitchIsChosenOnTheRadioAndTheArmSwitchAndSticksAreNotTaken()
        {
            CalibrationProfile p = Tx12Profile();
            var choice = new SwitchChoice(p, Tx12Stream.Axes);
            StringAssert.Contains("SA", choice.Prompt);
            long t = 1000000;
            void Feed(long us, int ch5, int ch6, int throttle = Low)
            {
                for (long end = t + us; t < end; t += ReportUs) choice.Feed(Report(t, ch5, ch6, 0, throttle));
            }

            Feed(500000, Low, Low);
            Feed(800000, High, Low); // the arm switch: refused and named
            Assert.AreEqual(SwitchStep.On, choice.Step);
            Assert.IsTrue(choice.ArmFlipped);
            StringAssert.Contains("arm switch (channel 5)", choice.Prompt);
            Feed(500000, Low, Low, High); // the throttle up: a stick, not a switch
            Assert.AreEqual(SwitchStep.On, choice.Step);
            Feed(100000, Low, High); // channel 6 under ArmHoldUs: not yet
            Assert.AreEqual(SwitchStep.On, choice.Step);
            Feed(300000, Low, High);
            Assert.AreEqual(SwitchStep.Off, choice.Step);
            Assert.AreEqual(ArmControl.Axis, choice.Result.Control);
            Assert.AreEqual(5, choice.Result.Index);
            Assert.IsTrue(choice.Result.OnHigh);
            Assert.That(choice.Result.Threshold, Is.InRange(1000, 1047));
            Feed(100000, Low, Low);
            Assert.AreEqual(SwitchStep.Off, choice.Step, "off under ArmHoldUs: not yet");
            Feed(300000, Low, Low);
            Assert.AreEqual(SwitchStep.Done, choice.Step);
            Assert.AreEqual("Restart on channel 6.", choice.Prompt);
            Assert.IsTrue(SwitchChoice.Free(p, choice.Result));
        }

        [Test]
        public void AButtonIsChosenAndFreeIsFalseForTheArmAndTheSticks()
        {
            CalibrationProfile p = Tx12Profile();
            var choice = new SwitchChoice(p, Tx12Stream.Axes);
            for (long t = 0; t < 2000000; t += ReportUs)
                choice.Feed(Report(t, buttons: t >= 500000 && t < 1200000 ? 1UL << 7 : 0)); // channel 16
            Assert.AreEqual(SwitchStep.Done, choice.Step);
            Assert.AreEqual(ArmControl.Button, choice.Result.Control);
            Assert.AreEqual(7, choice.Result.Index);
            Assert.AreEqual("Restart on button 8.", choice.Prompt);

            Assert.IsFalse(SwitchChoice.Free(p, p.Arm.Clone()), "the arm switch");
            Assert.IsFalse(SwitchChoice.Free(p, new ArmCalibration { Control = ArmControl.Axis, Index = 2 }), "the throttle");
            Assert.IsFalse(SwitchChoice.Free(p, new ArmCalibration()), "no switch");
            p.Arm = new ArmCalibration { Control = ArmControl.Button, Index = 7, OnHigh = true };
            Assert.IsFalse(SwitchChoice.Free(p, choice.Result), "an arm switch on the same button");
            Assert.IsTrue(SwitchChoice.Free(p, Ch6));
        }

        [Test]
        public void ASavedProfileReloadsItsRestartSwitch()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fpv450-restart-" + Guid.NewGuid().ToString("N"));
            try
            {
                CalibrationProfile p = Tx12Profile();
                p.Restart = Ch6.Clone();
                var store = new CalibrationStore(dir);
                store.Put(p);
                store.Save();
                string json = File.ReadAllText(store.FilePath);
                TestContext.WriteLine(json);
                StringAssert.Contains("\"restart\": { \"control\": \"axis\", \"index\": 5, \"threshold\": 1023, \"on_high\": true }", json);

                CalibrationStore back = CalibrationStore.Load(dir);
                ArmCalibration sw = back.ActiveProfile.Restart;
                Assert.AreEqual((Ch6.Control, Ch6.Index, Ch6.Threshold, Ch6.OnHigh), (sw.Control, sw.Index, sw.Threshold, sw.OnHigh));
                Assert.AreEqual(store.ToJson(), back.ToJson(), "the file reads back field for field");
                Assert.AreEqual(ArmControl.Axis, back.ActiveProfile.Arm.Control, "the arm switch is its own");
                Assert.AreEqual(4, back.ActiveProfile.Arm.Index);

                var flip = new SwitchFlip();
                long t = 0;
                Assert.AreEqual(0, Hold(flip, sw, false, 500000, ref t));
                Assert.AreEqual(1, Hold(flip, sw, true, 2000000, ref t), "the reloaded switch restarts once");

                // A file saved before the restart switch: its profile loads, with none.
                File.WriteAllText(store.FilePath, json.Replace(",\n      \"restart\": { \"control\": \"axis\", \"index\": 5, \"threshold\": 1023, \"on_high\": true }", ""));
                StringAssert.DoesNotContain("restart", File.ReadAllText(store.FilePath));
                CalibrationProfile old = CalibrationStore.Load(dir).ActiveProfile;
                Assert.AreEqual(ArmControl.None, old.Restart.Control);
                Assert.AreEqual(4, old.Arm.Index);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
