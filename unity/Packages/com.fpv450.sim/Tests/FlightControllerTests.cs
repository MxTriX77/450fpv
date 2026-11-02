using System;
using System.Globalization;
using System.IO;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // propulsion, "A Betaflight 4.5 flight controller in acro" (D-023, PHB-03): the ACTUAL rates against Betaflight's
    // formula, the defaults read from fc-profiles.md, Betaflight's scaling constants and throttle curve, the RC path's
    // settled setpoint, and the arming rules. The formulas below are written out from Betaflight 4.5.0's rc.c and
    // pid_init.c for the check, independently of FlightController's code.
    public sealed class FlightControllerTests
    {
        static readonly bool[] PropsIn = { true, false, false, true };

        static FlightController Controller() => new FlightController(FcSettings.Default, PropsIn);

        // rc.c applyActualRates after updateRcCommands' deadband, in System.Math, for the check.
        static double Actual(double stick, double deadband, double rcRate, double sRate, double expo)
        {
            double tmp = Math.Max(Math.Abs(500 * stick) - deadband, 0);
            double f = Math.Sign(stick) * tmp / (500 - deadband);
            double e = expo / 100, centre = rcRate * 10, movement = Math.Max(0, sRate * 10 - centre);
            return f * centre + movement * Math.Abs(f) * (Math.Pow(f, 5) * e + f * (1 - e));
        }

        [Test]
        public void ActualRatesAsBetaflightComputesThem()
        {
            FcSettings s = FcSettings.Default;
            FlightController fc = Controller();
            double[] sticks = { 0, 0.25, 0.5, 0.75, 1 };
            foreach (double x in sticks)
            {
                foreach (double sign in new[] { 1.0, -1.0 })
                {
                    double stick = sign * x;
                    Assert.That(fc.SetpointOf(0, stick), Is.EqualTo(Actual(stick, s.deadband, s.roll_rc_rate, s.roll_srate, s.roll_expo)).Within(0.1), "roll " + stick);
                    Assert.That(fc.SetpointOf(1, stick), Is.EqualTo(Actual(stick, s.deadband, s.pitch_rc_rate, s.pitch_srate, s.pitch_expo)).Within(0.1), "pitch " + stick);
                    Assert.That(fc.SetpointOf(2, stick), Is.EqualTo(Actual(stick, s.yaw_deadband, s.yaw_rc_rate, s.yaw_srate, s.yaw_expo)).Within(0.1), "yaw " + stick);
                }
            }
            // The sim default column's ends: the centre sensitivity and the full-stick rate.
            Assert.That(fc.SetpointOf(0, 1), Is.EqualTo(225).Within(1e-9));
            Assert.That(fc.SetpointOf(2, 1), Is.EqualTo(260).Within(1e-9));
            Assert.That(fc.SetpointOf(0, 0.002), Is.EqualTo(0), "inside the 2 µs deadband");
            double slope = (fc.SetpointOf(0, 0.0042) - fc.SetpointOf(0, 0.0041)) / 0.0001;
            Assert.That(slope, Is.EqualTo(55 * 500 / 498.0).Within(0.05), "the centre sensitivity, 55 °/s per full stick, over the deadband's divider");
            TestContext.Out.WriteLine("ACTUAL roll at 0, 0.25, 0.5, 0.75, 1: " + string.Join(", ", Array.ConvertAll(sticks, x => fc.SetpointOf(0, x).ToString("0.000", CultureInfo.InvariantCulture))) + " °/s");
            TestContext.Out.WriteLine("ACTUAL yaw  at 0, 0.25, 0.5, 0.75, 1: " + string.Join(", ", Array.ConvertAll(sticks, x => fc.SetpointOf(2, x).ToString("0.000", CultureInfo.InvariantCulture))) + " °/s");
        }

        // Through a frame and the RC smoothing: a held stick's smoothed setpoint settles on the curve.
        [Test]
        public void AHeldStickSettlesOnTheCurve()
        {
            FlightController fc = Controller();
            var s = new FcState();
            fc.Reset(ref s, default);
            bool armed = false;
            var stick = new StickSample { Roll = 0.5, Pitch = -0.75, Yaw = 0.25 };
            Corners duty = default;
            var loss = new MixerLoss();
            for (int loop = 0; loop < 2000; loop++)
            {
                if (FlightController.IsFrameLoop(loop)) fc.Frame(ref s, ref armed, stick);
                fc.Loop(ref s, armed, default, default, ref duty, ref loss);
            }
            Assert.That(s.Setpoint.Roll, Is.EqualTo(fc.SetpointOf(0, 0.5)).Within(0.1));
            Assert.That(s.Setpoint.Pitch, Is.EqualTo(fc.SetpointOf(1, -0.75)).Within(0.1));
            Assert.That(s.Setpoint.Yaw, Is.EqualTo(fc.SetpointOf(2, 0.25)).Within(0.1));
            Assert.That(s.Ff.Roll, Is.EqualTo(0).Within(1e-6), "a held stick has no feedforward");
            Assert.That(duty.RearRight, Is.EqualTo(0), "disarmed, the motors stop");
            Assert.That((fc.SetpointCutoffHz, fc.ThrottleCutoffHz, fc.FeedforwardCutoffHz), Is.EqualTo((62.0, 93.0, 62.0)),
                "rc.c's auto cutoffs at the 250 Hz link: 250 × 1.5/(1 + 50/10) and 250 × 1.5/(1 + 30/10), truncated");
        }

        // pid.h PTERM_SCALE, ITERM_SCALE, DTERM_SCALE, FEEDFORWARD_SCALE (Betaflight 4.5.0 lines 44–46 and 50) on the
        // builder's PIDs (pid_init.c pidInitConfig, lines 275–278: F also × 0.01).
        [Test]
        public void TheGainsCarryBetaflightsScaling()
        {
            FlightController fc = Controller();
            fc.Gains(0, out double kp, out double ki, out double kd, out double kf);
            Assert.That((kp, ki, kd, kf), Is.EqualTo((0.032029 * 65, 0.244381 * 20, 0.000529 * 70, 0.013754 * 0.5)));
            fc.Gains(1, out kp, out ki, out kd, out kf);
            Assert.That((kp, ki, kd, kf), Is.EqualTo((0.032029 * 60, 0.244381 * 20, 0.000529 * 65, 0.013754 * 0.5)));
            fc.Gains(2, out kp, out ki, out kd, out kf);
            Assert.That((kp, ki, kd, kf), Is.EqualTo((0.032029 * 45, 0.244381 * 30, 0.0, 0.0)));
        }

        // rc.c initRcProcessing's 12-point table in Betaflight's integer arithmetic, for thr_mid 50 and thr_expo 10,
        // worked by hand: 10·mid + tmp·(100 − expo + expo·tmp²/y²)/10 with C's truncating division.
        [Test]
        public void TheThrottleCurveIsBetaflightsTable()
        {
            int[] table = { 1000, 1116, 1221, 1318, 1410, 1500, 1590, 1682, 1779, 1884, 2000 };
            FlightController fc = Controller();
            for (int k = 0; k <= 10; k++)
            {
                double stick = (50 + 95 * k) / 1000.0; // rcData 1050 + 95k µs: min_check 1050 maps 1050–2000 onto 0–1000
                Assert.That(fc.ThrottleCommandOf(stick), Is.EqualTo(table[k]).Within(1e-9), "point " + k);
            }
            Assert.That(fc.ThrottleCommandOf(0.03), Is.EqualTo(1000), "below min_check the throttle reads zero");
            Assert.That(fc.ThrottleCommandOf(1), Is.EqualTo(2000));
            // DShot: the 5.5 % idle (dshot_idle_value 550) above 48, full at 2047.
            Assert.That(fc.DutyOf(0), Is.EqualTo(0.055).Within(1e-12));
            Assert.That(fc.DutyOf(1), Is.EqualTo(1).Within(1e-12));
        }

        // The builder's curve, thr_mid 100 and thr_expo 100 (fc-profiles §2, D-066): the same formula by hand,
        // 1000 + tmp³/1000 µs on tmp = 10k − 100, and the 12th point's zero divide no fault (the F405's SDIV gives 0).
        [Test]
        public void TheBuildersThrottleCurveIsBetaflightsTable()
        {
            int[] table = { 1000, 1271, 1488, 1657, 1784, 1875, 1936, 1973, 1992, 1999, 2000 };
            FcSettings s = FcSettings.Default;
            s.thr_mid = 100; s.thr_expo = 100;
            var fc = new FlightController(s, PropsIn);
            for (int k = 0; k <= 10; k++)
                Assert.That(fc.ThrottleCommandOf((50 + 95 * k) / 1000.0), Is.EqualTo(table[k]).Within(1e-9), "point " + k);
            Assert.That(fc.ThrottleCommandOf(1), Is.EqualTo(2000));
        }

        // D-023: the defaults are fc-profiles.md's sim default column and the builder's PIDs, read from the note.
        [Test]
        public void TheDefaultsAreTheProfilesNote()
        {
            string[] lines = File.ReadAllLines(TestFiles.Find("docs/reference-notes/fc-profiles.md"));
            FcSettings d = FcSettings.Default;
            Assert.That(SimDefault(lines, "roll / pitch centre"), Is.EqualTo(new[] { d.roll_rc_rate * 10 }));
            Assert.That(d.pitch_rc_rate, Is.EqualTo(d.roll_rc_rate));
            Assert.That(SimDefault(lines, "roll / pitch full stick"), Is.EqualTo(new[] { d.roll_srate * 10 }));
            Assert.That(d.pitch_srate, Is.EqualTo(d.roll_srate));
            Assert.That(SimDefault(lines, "roll / pitch expo"), Is.EqualTo(new[] { d.roll_expo / 100 }));
            Assert.That(d.pitch_expo, Is.EqualTo(d.roll_expo));
            Assert.That(SimDefault(lines, "yaw centre"), Is.EqualTo(new[] { d.yaw_rc_rate * 10 }));
            Assert.That(SimDefault(lines, "yaw full stick"), Is.EqualTo(new[] { d.yaw_srate * 10 }));
            Assert.That(SimDefault(lines, "yaw expo"), Is.EqualTo(new[] { d.yaw_expo / 100 }));
            Assert.That(SimDefault(lines, "`thr_mid`"), Is.EqualTo(new[] { d.thr_mid }));
            Assert.That(SimDefault(lines, "`thr_expo`"), Is.EqualTo(new[] { d.thr_expo }));
            Assert.That(SimDefault(lines, "`deadband` / `yaw_deadband`"), Is.EqualTo(new[] { d.deadband, d.yaw_deadband }));
            Assert.That(SimDefault(lines, "`rc_smoothing_auto_factor`"), Is.EqualTo(new[] { d.rc_smoothing_auto_factor }));
            Assert.That(SimDefault(lines, "`f_roll` / `f_pitch`"), Is.EqualTo(new[] { d.f_roll }));
            Assert.That(d.f_pitch, Is.EqualTo(d.f_roll));
            Assert.That(SimDefault(lines, "`f_yaw`"), Is.EqualTo(new[] { d.f_yaw }));
            Assert.That(SimDefault(lines, "`vbat_sag_compensation`"), Is.EqualTo(new[] { d.vbat_sag_compensation }));

            // §1's builder table: | axis | P | I | D | D min | Feedforward |.
            Assert.That(Builder(lines, "roll"), Is.EqualTo(new[] { d.p_roll, d.i_roll, d.d_roll, d.d_min_roll }));
            Assert.That(Builder(lines, "pitch"), Is.EqualTo(new[] { d.p_pitch, d.i_pitch, d.d_pitch, d.d_min_pitch }));
            Assert.That(Builder(lines, "yaw"), Is.EqualTo(new[] { double.NaN, d.i_yaw, d.d_yaw, double.NaN }), "yaw P is Betaflight's default");
            Assert.That(d.p_yaw, Is.EqualTo(45), "Betaflight 4.5.0's PID_YAW_DEFAULT P");
            StringAssert.Contains("`iterm_relax_cutoff = " + d.iterm_relax_cutoff + "`", string.Join("\n", lines));
            StringAssert.Contains("`throttle_boost = " + d.throttle_boost + "`", string.Join("\n", lines));

            // §1's filters, RPM filter and cell warning (1.4b), every one a biquad.
            string note = string.Join("\n", lines);
            string N(double x) => x.ToString(CultureInfo.InvariantCulture);
            Assert.That(new[] { d.gyro_lpf1_type, d.gyro_lpf2_type, d.dterm_lpf1_type, d.dterm_lpf2_type }, Is.All.EqualTo(LowpassType.BIQUAD));
            StringAssert.Contains($"gyro LPF1 dynamic {N(d.gyro_lpf1_dyn_min_hz)}–{N(d.gyro_lpf1_dyn_max_hz)} Hz (biquad) and LPF2 static {N(d.gyro_lpf2_static_hz)} Hz (biquad)", note);
            StringAssert.Contains($"a gyro notch at {N(d.gyro_notch1_hz)} Hz (cutoff {N(d.gyro_notch1_cutoff)})", note);
            StringAssert.Contains($"a dynamic notch at {N(d.dyn_notch_min_hz)}–{N(d.dyn_notch_max_hz)} Hz (Q {N(d.dyn_notch_q)})", note);
            StringAssert.Contains($"D-term LPF1 dynamic {N(d.dterm_lpf1_dyn_min_hz)}–{N(d.dterm_lpf1_dyn_max_hz)} Hz (biquad, static {N(d.dterm_lpf1_static_hz)}) "
                + $"and LPF2 {N(d.dterm_lpf2_static_hz)} Hz (biquad)", note);
            StringAssert.Contains($"a D-term notch at {N(d.dterm_notch_hz)} Hz (cutoff {N(d.dterm_notch_cutoff)})", note);
            StringAssert.Contains($"**DShot300, bidirectional**", note);
            Assert.That(d.dshot_bidir, Is.True);
            StringAssert.Contains($"`rpm_filter_min_hz = {N(d.rpm_filter_min_hz)}`", note);
            StringAssert.Contains($"{(d.vbat_warning_cell_voltage / 100).ToString("0.00", CultureInfo.InvariantCulture)} V warning", note);
        }

        // The arming rules (fc/core.c): the switch on at the start or with the throttle up refuses until it is off.
        [Test]
        public void BetaflightsArmingRules()
        {
            FlightController fc = Controller();
            var s = new FcState();
            fc.Reset(ref s, default);
            bool armed = false;
            var on = new StickSample { Arm = true };
            fc.Frame(ref s, ref armed, on);
            Assert.That(armed, Is.False, "the switch on from the start does not arm");
            fc.Frame(ref s, ref armed, new StickSample());
            fc.Frame(ref s, ref armed, on);
            Assert.That(armed, Is.True, "off, then on with the throttle low, arms");
            fc.Frame(ref s, ref armed, new StickSample { Arm = true, Throttle = 0.3 });
            Assert.That((s.Flags & FcFlags.AirmodeActive) != 0, Is.True, "airmode activates past 25 % throttle");
            fc.Frame(ref s, ref armed, new StickSample { Throttle = 0.3 });
            Assert.That(armed, Is.False, "the switch off disarms");
            Assert.That((s.Flags & FcFlags.AirmodeActive) != 0, Is.False);
            fc.Frame(ref s, ref armed, new StickSample { Arm = true, Throttle = 0.3 });
            Assert.That(armed, Is.False, "the throttle above min_check refuses");
            fc.Frame(ref s, ref armed, new StickSample { Arm = true });
            Assert.That(armed, Is.False, "and the refusal holds with the throttle back down, until the switch is off");
            fc.Frame(ref s, ref armed, new StickSample());
            fc.Frame(ref s, ref armed, on);
            Assert.That(armed, Is.True);
        }

        [Test]
        public void SettingsTheModelLacksAreRefused()
        {
            FcSettings s = FcSettings.Default;
            s.vbat_sag_compensation = 151;
            StringAssert.Contains("vbat_sag_compensation", Assert.Throws<ArgumentException>(() => new FlightController(s, PropsIn)).Message);
            s = FcSettings.Default;
            s.gyro_lpf2_type = LowpassType.PT1;
            StringAssert.Contains("gyro_lpf2_type", Assert.Throws<ArgumentException>(() => new FlightController(s, PropsIn)).Message);
            s.gyro_lpf2_static_hz = 0;
            Assert.DoesNotThrow(() => new FlightController(s, PropsIn), "a lowpass that is off may have any type");
            s = FcSettings.Default;
            s.dyn_notch_count = 6;
            StringAssert.Contains("dyn_notch_count", Assert.Throws<ArgumentException>(() => new FlightController(s, PropsIn)).Message);
            s = FcSettings.Default;
            s.feedforward_averaging = FeedforwardAveraging.TWO_POINT;
            StringAssert.Contains("feedforward_averaging", Assert.Throws<ArgumentException>(() => new FlightController(s, PropsIn)).Message);
            s = FcSettings.Default;
            s.thr_mid = 50.5;
            StringAssert.Contains("thr_mid", Assert.Throws<ArgumentException>(() => new FlightController(s, PropsIn)).Message);
        }

        // A table row's last cell, the sim default: "**55**", "**2 / 2**", "**0** (offered as an option)".
        static double[] SimDefault(string[] lines, string setting)
        {
            foreach (string line in lines)
            {
                if (!line.StartsWith("| " + setting, StringComparison.Ordinal)) continue;
                string[] cells = line.Trim().Trim('|').Split('|');
                string cell = cells[cells.Length - 1].Trim();
                int a = cell.IndexOf("**", StringComparison.Ordinal), b = cell.IndexOf("**", a + 2, StringComparison.Ordinal);
                string[] parts = cell.Substring(a + 2, b - a - 2).Split('/');
                var values = new double[parts.Length];
                for (int i = 0; i < parts.Length; i++) values[i] = double.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
                return values;
            }
            Assert.Fail("fc-profiles.md has no row " + setting);
            return null;
        }

        // §1's builder row: P, I, D, D min, NaN where the note gives no number.
        static double[] Builder(string[] lines, string axis)
        {
            foreach (string line in lines)
            {
                string[] cells = line.Trim().Trim('|').Split('|');
                if (cells.Length != 6 || cells[0].Trim() != axis) continue;
                var values = new double[4];
                for (int i = 0; i < 4; i++)
                    values[i] = double.TryParse(cells[i + 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
                return values;
            }
            Assert.Fail("fc-profiles.md has no builder row " + axis);
            return null;
        }
    }
}
