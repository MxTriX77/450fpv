using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Fpv450.App.Session;
using Fpv450.Feed;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.UI;
using NUnit.Framework;
using SimSignals = Fpv450.Sim.Flight.FeedSignals;

namespace Fpv450.Tests.Osd
{
    // The flight's OSD content model (p4-feed 1.5, OSD-03, D-057): the arm state's text, Betaflight 4.5's battery path
    // (the display filter, the warning's hysteresis, the current and mAh drawn) and its formats, on synthetic field means
    // (a stub pack) and on the physics' own signals in a keyboard flight.
    public sealed class FlightOsdTests
    {
        const float NoLinkLoss = float.PositiveInfinity;

        static FlightOsd Osd(int cells = 6) => new FlightOsd { Cells = cells };

        // A row of the grid from its pieces (column, text), blank elsewhere.
        static string Row(params (int col, string text)[] pieces)
        {
            var row = new string(' ', FeedOsd.Cols).ToCharArray();
            foreach ((int col, string text) in pieces)
                text.CopyTo(0, row, col, text.Length);
            return new string(row);
        }

        // ---- The arm state and the two strings --------------------------------------------------------------------------

        [Test]
        public void TheModeReadsAirArmedAndDisarmedOtherwise()
        {
            Assert.That(FlightOsd.Name, Is.EqualTo("Svinoriz 10 Opto"), "D-011: never revert");
            Assert.That(FlightOsd.Armament, Is.EqualTo("! SAFE !"), "D-011: never revert");
            Assert.That(FlightOsd.Name, Is.EqualTo(StubOsd.Name));
            Assert.That(FlightOsd.Armament, Is.EqualTo(StubOsd.Armed));

            FlightOsd osd = Osd();
            osd.Flight(false, 0, 0, 0);
            osd.Sample(0, 25.2, 0);
            osd.Build(0, NoLinkLoss);
            Assert.That(osd.Row(0).Substring(0, 9), Is.EqualTo("DISARMED "));
            Assert.That(osd.Row(10).Substring(0, 9), Is.EqualTo("! SAFE ! "), "the armament label, armed or not");
            Assert.That(osd.Row(12).Substring(7, 16), Is.EqualTo("SVINORIZ 10 OPTO"));

            osd.Flight(true, 0, 0, 0);
            osd.Build(4, NoLinkLoss);
            Assert.That(osd.Row(0).Substring(0, 9), Is.EqualTo("DISARMED "), "the text changes at the OSD's next refresh (12 Hz), not before");
            osd.Build(5, NoLinkLoss);
            Assert.That(osd.Row(0).Substring(0, 9), Is.EqualTo("AIR      "), "Betaflight's \"AIR \": the other cells clear");
            Assert.That(osd.Row(10).Substring(0, 9), Is.EqualTo("! SAFE ! "));
            Assert.That(osd.Row(12).Substring(7, 16), Is.EqualTo("SVINORIZ 10 OPTO"));

            osd.Flight(false, 0, 0, 0);
            osd.Build(9, NoLinkLoss);
            Assert.That(osd.Row(0).Substring(0, 9), Is.EqualTo("DISARMED "));
        }

        // ---- The layout and Betaflight's formats ------------------------------------------------------------------------

        // Every element in its cell: the footage's layout (StubOsd's) with the mode word, the mAh drawn and Betaflight's
        // number formats.
        [Test]
        public void EveryElementSitsInTheFootagesLayout()
        {
            FlightOsd osd = Osd();
            osd.Flight(true, 12.4, 250, 75.3);
            osd.Sample(1, 23.47, 41.2);
            osd.Build(0, NoLinkLoss);
            var expected = new string[FeedOsd.Rows];
            for (int r = 0; r < FeedOsd.Rows; r++) expected[r] = Row();
            expected[0] = Row((0, "AIR"), (23, ">12m"));
            expected[7] = Row((13, "{|}"));
            expected[10] = Row((0, "! SAFE !"));
            expected[11] = Row((0, "["), (1, "01:15"), (24, "0MAH")); // 41.2 A for 20 ms: 0.23 mAh, "%4d"
            expected[12] = Row((0, "$"), (1, "3.91в"), (7, "SVINORIZ 10 OPTO"), (24, "0.25")); // 2347 / 6 = 391
            expected[13] = Row((0, "$"), (1, "23.5в"), (22, "41.20"), (27, "a")); // 23.47 rounds to 23.5; " 41.20"
            for (int r = 0; r < FeedOsd.Rows; r++)
                Assert.That(osd.Row(r), Is.EqualTo(expected[r]), "row " + r);
            for (int r = 0; r < FeedOsd.Rows; r++)
                foreach (char c in osd.Row(r))
                    Assert.That(c == ' ' || FeedFont.Has(c), $"'{c}' in row {r} has no glyph");
        }

        [Test]
        public void ThePackTheCellAndTheCurrentPrintAsBetaflightDoes()
        {
            // osdElementMainBatteryVoltage: one decimal from 10 V, rounded half to even (lrintf); two below.
            Assert.That(FlightOsd.PackText(2520), Is.EqualTo("25.2"));
            Assert.That(FlightOsd.PackText(2525), Is.EqualTo("25.2"));
            Assert.That(FlightOsd.PackText(2535), Is.EqualTo("25.4"));
            Assert.That(FlightOsd.PackText(2549), Is.EqualTo("25.5"));
            Assert.That(FlightOsd.PackText(1000), Is.EqualTo("10.0"));
            Assert.That(FlightOsd.PackText(999), Is.EqualTo("9.99"));
            Assert.That(FlightOsd.PackText(985), Is.EqualTo("9.85"));

            // The cell: the pack's 0.01 V divided by the cells, truncated (getBatteryAverageCellVoltage), two decimals.
            FlightOsd osd = Osd();
            osd.Sample(1, 25.19, 12.349);
            Assert.That(osd.Now.PackCentivolts, Is.EqualTo(2519));
            Assert.That(osd.Now.CellCentivolts, Is.EqualTo(419), "4.198 V a cell reads 4.19, never 4.20");
            Assert.That(osd.Now.Centiamps, Is.EqualTo(1234), "osdElementCurrentDraw truncates");
            osd.Build(0, NoLinkLoss);
            Assert.That(osd.Row(12).Substring(0, 6), Is.EqualTo("$4.19в"));
            Assert.That(osd.Row(13).Substring(0, 6), Is.EqualTo("$25.2в"));
            Assert.That(osd.Row(13).Substring(21, 7), Is.EqualTo(" 12.34a"), "\"%3u.%02u\"");

            osd = Osd();
            osd.Sample(1, 25.2, 160.5);
            osd.Build(0, NoLinkLoss);
            Assert.That(osd.Row(13).Substring(21, 7), Is.EqualTo("160.50a"));
        }

        // ---- The filters ------------------------------------------------------------------------------------------------

        // vbat_display_lpf_period 30 and ibat_lpf_period 10: PT1s at 1/3 Hz and 1 Hz, run by the 50 Hz battery tasks, one
        // per field window; a frame within the same window runs nothing, and a frame that missed windows runs each.
        [Test]
        public void TheFiltersAreBetaflightsPt1sAtTheBatteryTasks()
        {
            // ω / (ω + 1), ω = 2π f dT: 2π · (1/3) · 0.02 = 0.041888 and 2π · 1 · 0.02 = 0.125664.
            double kv = FlightOsd.Pt1Gain(10.0 / FlightOsd.vbat_display_lpf_period), ka = FlightOsd.Pt1Gain(10.0 / FlightOsd.ibat_lpf_period);
            Assert.That(kv, Is.EqualTo(0.041888 / 1.041888).Within(1e-6));
            Assert.That(ka, Is.EqualTo(0.125664 / 1.125664).Within(1e-6));

            FlightOsd osd = Osd();
            osd.Sample(1, 25.2, 0);
            Assert.That(osd.Now.PackCentivolts, Is.EqualTo(2520), "the first sample sets the filter");
            double v = 25.2, a = 0;
            for (long w = 2; w <= 300; w++)
            {
                osd.Sample(w, 22.0, 120.0);
                osd.Sample(w, 22.0, 120.0); // a second frame in the same window
                v += kv * (22.0 - v);
                a += ka * (120.0 - a);
                Assert.That(osd.Now.PackCentivolts, Is.EqualTo((int)Math.Round(v * 100)), "window " + w);
                Assert.That(osd.Now.Centiamps, Is.EqualTo((int)Math.Floor(a * 100)), "window " + w);
            }
            // One time constant (0.477 s, 24 windows) after the step: 63 % of the sag shown.
            osd = Osd();
            osd.Sample(1, 25.2, 0);
            osd.Sample(25, 22.0, 0);
            Assert.That(osd.Now.PackCentivolts / 100.0, Is.EqualTo(25.2 - 3.2 * (1 - Math.Pow(1 - kv, 24))).Within(0.006));
            Assert.That((2520 - osd.Now.PackCentivolts) / 320.0, Is.EqualTo(0.63).Within(0.02));
        }

        // The spec scenario with a stub pack: the shown voltage falls within one OSD refresh of the sag.
        [Test]
        public void TheShownVoltageFallsWithinOneOsdRefreshOfTheSag()
        {
            FlightOsd osd = Osd();
            int sagAt = 40, fell = -1;
            for (int field = 0; field < 80; field++)
            {
                osd.Sample(field + 1, field < sagAt ? 25.2 : 22.0, field < sagAt ? 0 : 150);
                osd.Build(field, NoLinkLoss);
                if (field < sagAt) Assert.That(osd.Row(13).Substring(1, 4), Is.EqualTo("25.2"));
                else if (fell < 0 && osd.Row(13).Substring(1, 4) != "25.2") fell = field;
            }
            TestContext.WriteLine("the sag at field " + sagAt + " shows at field " + fell);
            Assert.That(fell - sagAt, Is.InRange(0, 4), "one 12 Hz refresh is 4.17 fields");
        }

        // osd_framerate_hz 12: the text takes the values at each refresh, 12 times in 50 fields.
        [Test]
        public void TheTextRefreshesAtTwelveHertz()
        {
            FlightOsd osd = Osd();
            var refreshed = new List<int>();
            for (int field = 0; field < 50; field++)
            {
                osd.Flight(false, field, 0, 0);
                osd.Build(field, NoLinkLoss);
                if (osd.Shown.AltitudeM == field) refreshed.Add(field);
            }
            Assert.That(refreshed, Is.EqualTo(new[] { 0, 5, 9, 13, 17, 21, 25, 30, 34, 38, 42, 46 }));
        }

        // ---- The warning ------------------------------------------------------------------------------------------------

        // batteryUpdateVoltageState: OK becomes WARNING at or below cells × vbat_warning_cell_voltage − vbat_hysteresis
        // (17.99 V on 6S), and WARNING becomes OK above cells × vbat_warning_cell_voltage (18.00 V): PackLow's threshold.
        [Test]
        public void TheWarningHasBetaflightsHysteresisAtPackLowsThreshold()
        {
            Assert.That(FlightOsd.vbat_warning_cell_voltage, Is.EqualTo((int)Math.Round(SimSignals.LowCellVolts * 100)), "PackLow's 3.00 V a cell");
            Assert.That(FlightOsd.vbat_hysteresis, Is.EqualTo(1));

            FlightOsd osd = Osd();
            osd.Sample(1, 18.00, 0);
            Assert.That(osd.Now.LowBattery, Is.False, "18.00 V is above 17.99");
            osd = Osd();
            osd.Sample(1, 17.99, 0);
            Assert.That(osd.Now.LowBattery, Is.True, "17.99 V sets it");
            long w = 1;
            for (int i = 0; i < 600; i++) osd.Sample(++w, 18.00, 0);
            Assert.That(osd.Now.PackCentivolts, Is.EqualTo(1800));
            Assert.That(osd.Now.LowBattery, Is.True, "back at 18.00 V it holds: OK needs above 18.00");
            for (int i = 0; i < 600; i++) osd.Sample(++w, 18.01, 0);
            Assert.That(osd.Now.PackCentivolts, Is.EqualTo(1801));
            Assert.That(osd.Now.LowBattery, Is.False, "18.01 V clears it");

            // 8S: the threshold scales with the cells.
            osd = Osd(8);
            osd.Sample(1, 23.99, 0);
            Assert.That(osd.Now.LowBattery, Is.True);
            osd = Osd(8);
            osd.Sample(1, 24.00, 0);
            Assert.That(osd.Now.LowBattery, Is.False);
        }

        // The warning shows "LOW BATTERY" over the name and both voltages blink with it, 12 fields on and 9 off.
        [Test]
        public void TheWarningBlinksOverTheNameWithTheVoltages()
        {
            FlightOsd osd = Osd();
            osd.Sample(1, 17.5, 30);
            osd.Build(11, NoLinkLoss);
            Assert.That(osd.Row(12).Substring(0, 23), Is.EqualTo("$2.91в   LOW BATTERY   "));
            Assert.That(osd.Row(13).Substring(0, 6), Is.EqualTo("$17.5в"));
            osd.Build(12, NoLinkLoss);
            Assert.That(osd.Row(12).Substring(0, 23), Is.EqualTo("       SVINORIZ 10 OPTO"), "off: the voltages blank, the name back");
            Assert.That(osd.Row(13).Substring(0, 6), Is.EqualTo("      "));
            Assert.That(osd.Row(13).Substring(21, 7), Is.EqualTo(" 30.00a"), "the current never blinks");
            osd.Build(21, NoLinkLoss);
            Assert.That(osd.Row(12).Substring(7, 16), Is.EqualTo("  LOW BATTERY   "));
            Assert.That(osd.Row(10).Substring(0, 8), Is.EqualTo("! SAFE !"));
        }

        // ---- mAh drawn --------------------------------------------------------------------------------------------------

        // updateCurrentmAhDrawnState: each window's current times 20 ms, from the first window on; whole mAh, "%4d"; a
        // frame that missed windows counts each at the latest current.
        [Test]
        public void TheChargeUsedIsTheIntegratedCurrent()
        {
            FlightOsd osd = Osd();
            double mAh = 0;
            for (long w = 1; w <= 3000; w++)
            {
                double amps = 3 + 0.25 * (w % 40) + (w > 1500 ? 140 : 0);
                osd.Sample(w, 25.0, amps);
                mAh += amps * 0.02 / 3.6;
                Assert.That(osd.Now.MahDrawn, Is.EqualTo((int)(mAh + 1e-9)).Or.EqualTo((int)(mAh - 1e-9)), "window " + w);
            }
            TestContext.WriteLine("60 s: " + osd.Now.MahDrawn + " mAh, " + mAh.ToString("0.000") + " integrated");
            osd.Sample(3010, 25.0, 100);
            mAh += 10 * 100 * 0.02 / 3.6;
            Assert.That(osd.Now.MahDrawn, Is.EqualTo((int)mAh), "ten windows missed: each at the latest current");
            osd.Build(0, NoLinkLoss);
            Assert.That(osd.Row(11).Substring(21, 7), Is.EqualTo(((int)mAh).ToString().PadLeft(4) + "MAH"));

            osd.Reset();
            osd.Build(5, NoLinkLoss);
            Assert.That(osd.Row(11).Substring(21, 7), Is.EqualTo("       "), "a new pack: blank until its first sample");
            osd.Sample(1, 25.2, 0);
            osd.Build(9, NoLinkLoss);
            Assert.That(osd.Row(11).Substring(21, 7), Is.EqualTo("   0MAH"));
        }

        // ---- On the physics' signals ------------------------------------------------------------------------------------

        // A keyboard flight on the floor from a nearly empty pack (8 % charge): rest, armed, then a full-collective pull.
        // Each field window, the OSD's values are Betaflight's path on the published field means (the cells and the
        // threshold PackLow's); mAh drawn is the packs' own charge used at 1 kHz; the warning sets after PackLow; and the
        // shown pack falls within one refresh of the sag.
        [Test]
        public void TheOsdReadsThePhysicsSignalsAsBetaflight()
        {
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(RepositoryFile("assets-src/drone/drone.json")));
            MaterialRow[] materials = PhysicsMaterials.Parse(File.ReadAllText(RepositoryFile(PhysicsMaterials.RelativePath)), out ulong hash);
            const double start = 0.08;
            string dir = Path.Combine(Path.GetTempPath(), "fpv450-osd-" + Guid.NewGuid().ToString("N"));
            var clock = new ManualClock { Now = 1000000 };
            var reader = new RadioReader(() => null);
            var session = new FlightSession(new SessionInputs
            {
                Drone = spec,
                Materials = materials,
                MaterialsHash = hash,
                LogFolder = dir,
                Build = "osdtests",
                Reader = reader,
                Clock = clock,
                Threaded = false,
                KeyboardAllowed = true,
                NewSeed = () => 0xD057,
                Adjust = s => s.StartPackCharge = start,
            });
            FlightOsd osd = Osd(spec.Packs[0].Series);
            double kv = FlightOsd.Pt1Gain(1.0 / 3), ka = FlightOsd.Pt1Gain(1);
            double capacity = spec.Packs[0].Parallel * spec.Packs[0].CellCapacity * 3600; // A·s a pack; both alike
            double v = 0, a = 0, mAh = 0, worstMah = 0, restShown = 0, lowestRaw = double.MaxValue;
            bool sampled = false, low = false, sawDisarmed = false, sawAir = false;
            long window = 0, firstPackLow = -1, firstWarning = -1, sagAt = -1, fellAt = -1;
            try
            {
                for (int ms = 0; ms < 4000 && session.State != SessionState.Ended; ms++)
                {
                    if (ms == 10) session.Start();
                    KeyboardKeys keys = ms == 60 ? KeyboardKeys.ArmToggle : ms >= 200 && ms < 2200 ? KeyboardKeys.ThrottleUp : KeyboardKeys.None;
                    clock.Now += FlightCore.StepUs;
                    reader.Pump(0);
                    session.Tick(keys);
                    if (session.Flights == 0) continue;
                    ref readonly PosePair pair = ref session.Poses.Read();
                    SimSignals s = pair.Signals;
                    if (pair.StartUs < session.StartUs || s.Step < FlightCore.FieldSteps) continue; // the first whole field
                    Assert.That((s.Live & FeedSignalSet.PackVolts) != 0 && (s.Live & FeedSignalSet.MotorCurrent) != 0);
                    if (s.Step / FlightCore.FieldSteps == window) continue;

                    // A new window: Betaflight's path, by hand, on its means.
                    window = s.Step / FlightCore.FieldSteps;
                    if (!sampled)
                    {
                        sampled = true;
                        v = s.PackVolts;
                        a = s.MotorCurrentTotalA;
                    }
                    else
                    {
                        v += kv * (s.PackVolts - v);
                        a += ka * (s.MotorCurrentTotalA - a);
                    }
                    mAh += s.MotorCurrentTotalA * 0.02 / 3.6;
                    int cv = (int)Math.Round(v * 100), warning = osd.Cells * FlightOsd.vbat_warning_cell_voltage;
                    low = low ? cv <= warning : cv <= warning - 1;
                    osd.Flight(pair.Current.Armed, 0, 0, s.Step * FlightCore.Dt);
                    osd.Sample(window, s.PackVolts, s.MotorCurrentTotalA);
                    osd.Build((int)window, NoLinkLoss);

                    Assert.That(osd.Now.PackCentivolts, Is.EqualTo(cv), "window " + window);
                    Assert.That(osd.Now.CellCentivolts, Is.EqualTo(cv / osd.Cells));
                    Assert.That(osd.Now.Centiamps, Is.EqualTo((int)Math.Floor(a * 100)));
                    Assert.That(osd.Now.LowBattery, Is.EqualTo(low), "window " + window);
                    Assert.That(osd.Now.MahDrawn, Is.EqualTo((int)mAh).Or.EqualTo((int)mAh - 1).Or.EqualTo((int)mAh + 1));

                    // The packs' charge used, the physics' own 1 kHz integral of their currents, at this window's end.
                    FlightState state = session.Core.State;
                    Assert.That(state.Step, Is.EqualTo(s.Step));
                    double used = ((start - state.PackCharge0) + (start - state.PackCharge1)) * capacity / 3.6;
                    worstMah = Math.Max(worstMah, Math.Abs(mAh - used));
                    Assert.That(osd.Now.MahDrawn, Is.InRange((int)Math.Floor(used - 0.01), (int)Math.Floor(used + 0.01)), "window " + window);

                    Assert.That(osd.Row(0).Substring(0, 9), Is.EqualTo(osd.Shown.Armed ? "AIR      " : "DISARMED "), "window " + window);
                    sawDisarmed |= !pair.Current.Armed && osd.Row(0).StartsWith("DISARMED ", StringComparison.Ordinal);
                    sawAir |= pair.Current.Armed && osd.Row(0).StartsWith("AIR      ", StringComparison.Ordinal);
                    if (s.PackLow && firstPackLow < 0) firstPackLow = window;
                    if (osd.Now.LowBattery && firstWarning < 0) firstWarning = window;
                    if (window == 5) restShown = osd.Shown.PackCentivolts;
                    lowestRaw = Math.Min(lowestRaw, s.PackVolts);
                    if (sagAt < 0 && window > 5 && s.PackVolts < restShown / 100.0 - 0.1) sagAt = window;
                    if (sagAt >= 0 && fellAt < 0 && osd.Shown.PackCentivolts < restShown) fellAt = window;
                }
                Assert.That(session.Core.Propulsion.SeriesCells, Is.EqualTo(osd.Cells), "the OSD's cells are PackLow's");
                Assert.That(sawDisarmed && sawAir, "DISARMED at rest, AIR once armed");
                TestContext.WriteLine($"rest {restShown / 100:0.00} V, lowest field mean {lowestRaw:0.00} V; PackLow at window {firstPackLow}, " +
                                      $"the warning at {firstWarning}; the sag at {sagAt}, shown at {fellAt}; {osd.Now.MahDrawn} mAh drawn, " +
                                      $"the field means' integral within {worstMah:0.000000} mAh of the packs' charge used");
                Assert.That(firstPackLow, Is.GreaterThan(0), "the pull takes the pack below 3.00 V a cell");
                Assert.That(firstWarning, Is.GreaterThanOrEqualTo(firstPackLow), "the warning follows PackLow, through the filter");
                Assert.That(firstWarning - firstPackLow, Is.LessThan(100), "within 2 s");
                Assert.That(worstMah, Is.LessThan(0.01));
                Assert.That(fellAt - sagAt, Is.InRange(0, 5), "the shown pack falls within one refresh of the sag");
                Assert.That(osd.Now.MahDrawn, Is.GreaterThan(10));
            }
            finally
            {
                session.Dispose();
                reader.Dispose();
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        sealed class ManualClock : IHostClock
        {
            public long Now;
            public long NowUs() => Now;
        }

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
