using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Fpv450.Input.Radio;
using Fpv450.Sim.Control;
using NUnit.Framework;

namespace Fpv450.Tests.Radio
{
    // tx-input (p6-input-app 1.3, INP-03, INP-07, APP-05): the defaults, the player default column (D-066), through the
    // settings file and the screen's rows, the previews against the flight controller's own RC path and against
    // Betaflight's formula written out here, and the settings file: a restart reads it back bit for bit, a corrupt one
    // falls back to the defaults with a message.
    public sealed class RatesTests
    {
        static readonly bool[] Spins = { true, false, false, true };

        static FcSettingRow Row(string name) => FcSettingRow.Rows.Single(r => r.Name == name);

        static string TempDir() => Path.Combine(Path.GetTempPath(), "fpv450-fc-settings-" + Guid.NewGuid().ToString("N"));

        static void Clean(string dir)
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        // Every FcSettings field equal, doubles bit for bit.
        static void AssertSame(FcSettings expected, FcSettings actual, string what)
        {
            object e = expected, a = actual;
            foreach (FieldInfo f in typeof(FcSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object x = f.GetValue(e), y = f.GetValue(a);
                if (x is double dx)
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(dx), BitConverter.DoubleToInt64Bits((double)y), what + ": " + f.Name + " " + dx.ToString("R", CultureInfo.InvariantCulture) + " against " + ((double)y).ToString("R", CultureInfo.InvariantCulture));
                else Assert.AreEqual(x, y, what + ": " + f.Name);
            }
        }

        // rc.c applyActualRates after updateRcCommands' deadband, in System.Math, for the check (as the sim's
        // FlightControllerTests writes it out, independently of FlightController's code).
        static double Actual(double stick, double deadband, double rcRate, double sRate, double expo)
        {
            double tmp = Math.Max(Math.Abs(500 * stick) - deadband, 0);
            double f = Math.Sign(stick) * tmp / (500 - deadband);
            double e = expo / 100, centre = rcRate * 10, movement = Math.Max(0, sRate * 10 - centre);
            return f * centre + movement * Math.Abs(f) * (Math.Pow(f, 5) * e + f * (1 - e));
        }

        [Test]
        public void AFreshProfileIsThePlayerDefault()
        {
            string dir = TempDir();
            try
            {
                FcSettings s = FcSettingsStore.Load(dir, out string error);
                Assert.IsNull(error, "no file yet is no error");
                Assert.IsFalse(File.Exists(FcSettingsStore.PathIn(dir)), "loading writes nothing");
                AssertSame(FcSettingsStore.PlayerDefault, s, "no file: FcSettingsStore.PlayerDefault");

                // The screen's rows, in the configurator's units: the pilot's FpvPrep 10-inch profile's rates, and the
                // builder's throttle curve without FpvPrep's "throttle softer" (D-066).
                double Shown(string name) => Row(name).Shown(s);
                Assert.AreEqual(new[] { 50.0, 220, 0.45 }, new[] { Shown("roll_rc_rate"), Shown("roll_srate"), Shown("roll_expo") }, "roll");
                Assert.AreEqual(new[] { 50.0, 220, 0.45 }, new[] { Shown("pitch_rc_rate"), Shown("pitch_srate"), Shown("pitch_expo") }, "pitch");
                Assert.AreEqual(new[] { 80.0, 240, 0.2 }, new[] { Shown("yaw_rc_rate"), Shown("yaw_srate"), Shown("yaw_expo") }, "yaw");
                Assert.AreEqual(new[] { 100.0, 100 }, new[] { s.thr_mid, s.thr_expo }, "thr_mid, thr_expo (CLI)");
                Assert.AreEqual(new[] { 1.0, 1.0 }, new[] { Shown("thr_mid"), Shown("thr_expo") }, "thr_mid, thr_expo (shown)");
                // Off the screen's rows, every setting is the sim core's default.
                object player = s, core = FcSettings.Default;
                foreach (FieldInfo f in typeof(FcSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
                    if (!FcSettingRow.Rows.Any(r => r.Name == f.Name)) Assert.AreEqual(f.GetValue(core), f.GetValue(player), f.Name + " is FcSettings.Default's");
                // The rest, not on the screen in v1: the sim core's defaults.
                Assert.AreEqual(new[] { 2.0, 2 }, new[] { s.deadband, s.yaw_deadband }, "deadband / yaw_deadband");
                Assert.IsTrue(s.rc_smoothing);
                Assert.AreEqual(50, s.rc_smoothing_auto_factor, "rc_smoothing_auto_factor");
                Assert.AreEqual(new[] { 50.0, 50, 0 }, new[] { s.f_roll, s.f_pitch, s.f_yaw }, "feedforward");
                // The profile is silent on airmode, and its min_throttle is analog's (DShot ignores it): Betaflight
                // 4.5's AIRMODE feature and dshot_idle_value, labelled in FcSettings.
                Assert.IsTrue(s.airmode, "airmode");
                Assert.AreEqual(550, s.dshot_idle_value, "dshot_idle_value");

                TestContext.WriteLine("defaults as the screen reads them:");
                foreach (FcSettingRow r in FcSettingRow.Rows)
                    TestContext.WriteLine("  " + r.Group + " " + r.Label + " = " + r.Shown(s).ToString(r.Format, CultureInfo.InvariantCulture) + r.Unit
                        + "   (" + r.Name + " " + r.Get(s).ToString("R", CultureInfo.InvariantCulture) + ")");
                TestContext.WriteLine("  deadband " + s.deadband + " / yaw_deadband " + s.yaw_deadband + ", rc_smoothing_auto_factor " + s.rc_smoothing_auto_factor
                    + ", f_roll / f_pitch / f_yaw " + s.f_roll + " / " + s.f_pitch + " / " + s.f_yaw + ", airmode " + s.airmode + ", dshot_idle_value " + s.dshot_idle_value);
            }
            finally
            {
                Clean(dir);
            }
        }

        // The preview is the controller's curve: at every sampled stick, the setpoint a frame of the flight's RC path
        // computes (FlightController.Frame's rawSetpoint) and Betaflight's formula, each within 0.1 °/s; the throttle as
        // the frame's rcCommand.
        [Test]
        public void ThePreviewIsTheControllersCurve()
        {
            FcSettings edited = FcSettings.Default;
            edited.roll_rc_rate = 7; edited.roll_srate = 30; edited.roll_expo = 50;
            edited.pitch_rc_rate = 4.5; edited.pitch_srate = 18; edited.pitch_expo = 0;
            edited.yaw_rc_rate = 10; edited.yaw_srate = 8; edited.yaw_expo = 100; // max under the centre: ACTUAL keeps the centre's line
            edited.deadband = 5; edited.yaw_deadband = 10;
            edited.thr_mid = 40; edited.thr_expo = 30;

            foreach ((string name, FcSettings s) in new[] { ("defaults", FcSettingsStore.PlayerDefault), ("edited", edited) })
            {
                var preview = new RatePreview();
                preview.From(s);
                var fc = new FlightController(s, Spins);
                double[] rc = { s.roll_rc_rate, s.pitch_rc_rate, s.yaw_rc_rate }, sr = { s.roll_srate, s.pitch_srate, s.yaw_srate };
                double[] ex = { s.roll_expo, s.pitch_expo, s.yaw_expo }, db = { s.deadband, s.deadband, s.yaw_deadband };
                double worstFrame = 0, worstFormula = 0, worstThrottleUs = 0;
                for (int i = 0; i < RatePreview.Points; i++)
                {
                    double x = RatePreview.Stick(i);
                    var st = new FcState();
                    fc.Reset(ref st, default);
                    bool armed = false;
                    fc.Frame(ref st, ref armed, new StickSample { Roll = x, Pitch = x, Yaw = x, Throttle = x });
                    for (int a = 0; a < 3; a++)
                    {
                        worstFrame = Math.Max(worstFrame, Math.Abs(preview.Rate[a][i] - st.RawSetpoint[a]));
                        worstFormula = Math.Max(worstFormula, Math.Abs(preview.Rate[a][i] - Actual(x, db[a], rc[a], sr[a], ex[a])));
                    }
                    worstThrottleUs = Math.Max(worstThrottleUs, Math.Abs(1000 + 1000 * preview.Throttle[i] - st.RcThrottle));
                }
                string At(double[] curve) => string.Join(", ", new[] { 0, 25, 50, 75, 100 }.Select(i => curve[i].ToString("0.000", CultureInfo.InvariantCulture)));
                TestContext.WriteLine(name + ": " + RatePreview.Points + " sticks 0…1 an axis; worst |preview − frame| " + worstFrame.ToString("G3", CultureInfo.InvariantCulture)
                    + " °/s, |preview − Betaflight's formula| " + worstFormula.ToString("G3", CultureInfo.InvariantCulture)
                    + " °/s, throttle |preview − rcCommand| " + worstThrottleUs.ToString("G3", CultureInfo.InvariantCulture) + " µs");
                TestContext.WriteLine("  at 0, 0.25, 0.5, 0.75, 1: roll " + At(preview.Rate[0]) + " | pitch " + At(preview.Rate[1]) + " | yaw " + At(preview.Rate[2])
                    + " °/s | throttle " + At(preview.Throttle));
                Assert.That(worstFrame, Is.LessThanOrEqualTo(0.1), name + ": the flight's RC frame");
                Assert.That(worstFormula, Is.LessThanOrEqualTo(0.1), name + ": Betaflight's formula");
                Assert.That(worstThrottleUs, Is.LessThanOrEqualTo(0.1), name + ": the throttle, µs");
            }

            // The ends of the default curves are the column's full-stick rates; the throttle reaches full.
            var d = new RatePreview();
            d.From(FcSettingsStore.PlayerDefault);
            Assert.That(d.Rate[0][RatePreview.Points - 1], Is.EqualTo(220).Within(1e-9));
            Assert.That(d.Rate[2][RatePreview.Points - 1], Is.EqualTo(240).Within(1e-9));
            Assert.That(d.Throttle[RatePreview.Points - 1], Is.EqualTo(1).Within(1e-12));
        }

        // "Settings survive a restart" for the rates: the screen's edits on its steps, and settings the screen does not
        // show yet (INP-07: a double that needs 17 digits, a bool, an enum), saved, then read afresh as the next start
        // reads them: every FcSettings field bit for bit.
        [Test]
        public void SettingsSurviveARestartBitForBit()
        {
            string dir = TempDir();
            try
            {
                FcSettings s = FcSettingsStore.Load(dir, out _);
                void Edit(string name, double cli)
                {
                    FcSettingRow r = Row(name);
                    r.Set(ref s, r.Snap(cli));
                }
                Edit("roll_rc_rate", 6.1);   // 60 °/s on the screen's 5 °/s step
                Edit("roll_srate", 24.8);    // 250 °/s
                Edit("pitch_expo", 45.4);    // 0.45
                Edit("yaw_srate", 28.5);     // 285 °/s
                Edit("thr_mid", 45);
                Edit("thr_expo", 25);
                Assert.AreEqual(new[] { 6.0, 25, 45, 28.5, 45, 25 }, new[] { s.roll_rc_rate, s.roll_srate, s.pitch_expo, s.yaw_srate, s.thr_mid, s.thr_expo });
                s.anti_gravity_p_gain = 100.0 / 3;
                s.airmode = false;
                s.tpa_mode = TpaMode.PD;

                FcSettingsStore.Save(dir, s);
                string path = FcSettingsStore.PathIn(dir), text = File.ReadAllText(path);
                TestContext.WriteLine(path.Substring(dir.Length + 1) + ":\n" + text);

                FcSettings back = FcSettingsStore.Load(dir, out string error);
                Assert.IsNull(error);
                AssertSame(s, back, "after a restart");
                Assert.AreEqual(text, FcSettingsStore.ToJson(back), "written again, the same file");
                StringAssert.Contains("\"roll_rc_rate\": 6,", text);
                StringAssert.Contains("\"anti_gravity_p_gain\": 33.33333333333333", text);
                StringAssert.DoesNotContain("\"p_roll\"", text, "a default the screen does not show is left to the defaults");

                FcSettingsStore.Save(dir, back); // the swap over an existing file
                Assert.AreEqual(new[] { FcSettingsStore.FileName }, Directory.GetFiles(dir).Select(Path.GetFileName).ToArray(), "no temporary file left");
                AssertSame(s, FcSettingsStore.Load(dir, out error), "after a second save");
                Assert.IsNull(error);
            }
            finally
            {
                Clean(dir);
            }
        }

        [Test]
        public void ACorruptFileFallsBackToTheDefaultsWithAMessage()
        {
            string Settings(string body) => "{\n  \"format\": \"fpv450 fc settings 1\",\n  \"settings\": {\n    " + body + "\n  }\n}\n";
            (string what, string text, string message)[] cases =
            {
                ("cut short", "{\n  \"format\": \"fpv450 fc settings 1\",\n  \"settings\": {\n    \"roll_rc_rate\": 5.5,", "JSON line"),
                ("not text", "\u0001\u0002\u0003", "JSON line 1"),
                ("another file", "{ \"format\": \"fpv450 calibration 1\", \"settings\": {} }", "is not fpv450 fc settings 1"),
                ("out of range", Settings("\"roll_rc_rate\": 300"), "'roll_rc_rate' 300 is outside Betaflight's 1–200"),
                ("not whole", Settings("\"thr_mid\": 50.5"), "'thr_mid' 50.5 is not a whole number"),
                ("a name Betaflight lacks", Settings("\"rol_rc_rate\": 5.5"), "no Betaflight setting 'rol_rc_rate'"),
                ("a mistyped value", Settings("\"airmode\": 1"), "'airmode' is not true or false"),
                ("refused by the controller", Settings("\"feedforward_averaging\": \"TWO_POINT\""), "the flight controller refuses them: feedforward_averaging"),
            };
            string dir = TempDir();
            try
            {
                Directory.CreateDirectory(dir);
                foreach ((string what, string text, string message) in cases)
                {
                    File.WriteAllText(FcSettingsStore.PathIn(dir), text);
                    FcSettings s = FcSettingsStore.Load(dir, out string error);
                    TestContext.WriteLine(what + ": " + error);
                    AssertSame(FcSettingsStore.PlayerDefault, s, what);
                    Assert.IsNotNull(error, what);
                    StringAssert.StartsWith(FcSettingsStore.FileName + " could not be read", error, what);
                    StringAssert.Contains(message, error, what);
                    StringAssert.EndsWith("the defaults are in use, and the next change replaces it.", error, what);
                    Assert.AreEqual(text, File.ReadAllText(FcSettingsStore.PathIn(dir)), what + ": the file is left as it was until a change is saved");
                }
            }
            finally
            {
                Clean(dir);
            }
        }
    }
}
