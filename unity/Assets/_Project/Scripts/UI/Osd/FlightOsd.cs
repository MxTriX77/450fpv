using System;
using System.Globalization;
using Fpv450.Feed;
using Fpv450.Sim.Flight;

namespace Fpv450.UI
{
    /// <summary>
    /// The flight's OSD content model (p4-feed design 5, task 1.5; OSD-03, D-057): which elements the OSD shows, where,
    /// and from which signal. The tech-artist's feed draws the grid (FeedOsd, FeedFont); this fills it, once per video
    /// field, in the footage's layout (the Godot OSD's, ported as StubOsd), with Betaflight 4.5's OSD behaviour.
    /// </summary>
    /// <remarks>
    /// <para>The elements (row, columns), from the flight (FlightScene) and the physics' FeedSignals:</para>
    /// <list type="bullet">
    /// <item>0, from 0: the flight mode, "AIR" while armed and "DISARMED" while disarmed (D-057), from the pose's arm state.</item>
    /// <item>0, to 26: the altitude, "&gt;" and whole metres above the rest pose.</item>
    /// <item>7, 13–15: the reticle.</item>
    /// <item>10, 0–7: the armament label "! SAFE !" (D-011), always.</item>
    /// <item>11, 0–5: the flight timer, MM:SS of flight time.</item>
    /// <item>11, to 27: the charge used, mAh, integrated from the current (Betaflight's mAh drawn), its unit "MAH".</item>
    /// <item>12, 0–5: a battery mark and the average cell voltage; 7–22: the name "Svinoriz 10 Opto" (D-011), or a warning
    /// over it; to 27: the distance home, km.</item>
    /// <item>13, 0–5: a battery mark and the pack voltage; to 26: the current, A, and its unit at 27.</item>
    /// </list>
    /// <para>Betaflight's battery path, at its 50 Hz battery tasks, one per 20 ms field window of sim time (FeedSignals'
    /// field means: PackVolts, MotorCurrentTotalA):</para>
    /// <list type="bullet">
    /// <item>The voltage: a PT1 at 10 / vbat_display_lpf_period Hz, in 0.01 V. The pack shows it rounded, one decimal
    /// from 10 V up and two below; the cell shows it divided by the cells (integer division), two decimals.</item>
    /// <item>The current: a PT1 at 10 / ibat_lpf_period Hz (the ADC current meter), truncated to 0.01 A, "%3u.%02u".</item>
    /// <item>mAh drawn: the unfiltered current times each update's interval, truncated to whole mAh, "%4d".</item>
    /// <item>The warning: Betaflight's state machine on the filtered voltage. OK becomes WARNING at or below
    /// cells × vbat_warning_cell_voltage − vbat_hysteresis, and WARNING becomes OK above cells × vbat_warning_cell_voltage.
    /// The threshold and the cells are PackLow's (FeedSignals.LowCellVolts, Propulsion.SeriesCells), so the warning
    /// follows PackLow through the filter and the hysteresis (D-048). It shows "LOW BATTERY" over the name, and the two
    /// voltages blink with it (osdUpdateAlarms).</item>
    /// </list>
    /// <para>Both filters start at their first sample after Reset, as a flight controller's have settled by the time a
    /// pack is flown; until it the battery elements are blank. The text is refreshed at osd_framerate_hz; blinking keeps
    /// the footage's 12 fields on and 9 off.</para>
    /// <para>Never revert "Svinoriz 10 Opto" or "! SAFE !" (D-011, SEC-05). Every value comes from the sim, none from
    /// footage.</para>
    /// </remarks>
    public sealed class FlightOsd
    {
        // Betaflight 4.5's battery and OSD settings, by their CLI names (D-023): the pilot's profile where it sets one
        // (docs/reference-notes/fc-profiles.md), Betaflight's default otherwise.
        /// <summary>0.01 V a cell: the pilot's 3.00 V, Li-ion; FeedSignals.LowCellVolts.</summary>
        public const int vbat_warning_cell_voltage = 300;
        /// <summary>0.01 V on the pack: the default.</summary>
        public const int vbat_hysteresis = 1;
        /// <summary>0.1 s, the display filter's period: the default, 0.33 Hz.</summary>
        public const int vbat_display_lpf_period = 30;
        /// <summary>0.1 s, the current filter's period: the default, 1 Hz.</summary>
        public const int ibat_lpf_period = 10;
        /// <summary>The OSD's refresh, Hz: the default.</summary>
        public const int osd_framerate_hz = 12;

        /// <summary>The name and the armament label, D-011: never revert either.</summary>
        public const string Name = "Svinoriz 10 Opto", Armament = "! SAFE !";
        /// <summary>The flight-mode element's two words (D-057).</summary>
        public const string ArmedMode = "AIR", DisarmedMode = "DISARMED";
        /// <summary>Betaflight's battery warning text (osd_warnings.c).</summary>
        public const string LowBattery = "LOW BATTERY";
        /// <summary>The mAh unit, spelled out: the ROM has no one-cell mAh mark.</summary>
        public const string MahUnit = "MAH";

        /// <summary>The cells in series: Propulsion.SeriesCells, PackLow's.</summary>
        public int Cells;

        /// <summary>The grid, [row, column]; blank until the first Build.</summary>
        public readonly char[,] Grid = new char[FeedOsd.Rows, FeedOsd.Cols];

        /// <summary>What the OSD shows at its latest refresh: the values behind the text.</summary>
        public struct Readout
        {
            public bool Armed, LowBattery;
            /// <summary>The battery path has its first sample; until then the battery elements are blank.</summary>
            public bool Sampled;
            /// <summary>The filtered pack voltage, 0.01 V, and the average cell's (integer division by the cells).</summary>
            public int PackCentivolts, CellCentivolts;
            /// <summary>The filtered current, 0.01 A, truncated.</summary>
            public int Centiamps;
            public int MahDrawn;
            public double AltitudeM, DistanceM, ElapsedSeconds;
        }

        /// <summary>The values at the OSD's latest refresh, and now.</summary>
        public Readout Shown => _shown;
        public Readout Now => _now;

        const int FieldHz = 50;
        const double WindowS = FlightCore.FieldSteps * FlightCore.Dt;
        const double TwoPi = 6.283185307179586;
        /// <summary>A frame that missed this many windows has let both filters settle (over 10 time constants).</summary>
        const long SettleWindows = 250;
        /// <summary>Warning elements blink 12 fields on and 9 off, as the footage does (FeedOsd.cs:55).</summary>
        const int BlinkOnFields = 12, BlinkOffFields = 9;
        /// <summary>The stand-in link warning's threshold, as the Godot OSD's (FeedOsd.cs:76), until 7d's link.</summary>
        const float LowLinkFraction = 0.35f;
        const int RowTop = 0, RowReticle = 7, RowArmed = 10, RowTimer = 11, RowCell = 12, RowPack = 13;
        const int ColLeft = 0, ColStack = 1, ColReticle = 13, ColName = 7, ColAltEnd = 26, ColRightEnd = 27, ColAmpsEnd = 26;
        static readonly string Airframe = Name.ToUpperInvariant();
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        static readonly double VoltsGain = Pt1Gain(10.0 / vbat_display_lpf_period);
        static readonly double AmpsGain = Pt1Gain(10.0 / ibat_lpf_period);

        long _window;
        double _volts, _amps, _mAh;
        bool _low;
        Readout _now, _shown;
        long _refresh;

        public FlightOsd()
        {
            Reset();
            Clear();
        }

        /// <summary>A new pack: the filters, the charge used and the warning start again, and the next Build refreshes.</summary>
        public void Reset()
        {
            _mAh = 0;
            _low = false;
            _now = default;
            _refresh = long.MinValue;
        }

        /// <summary>Main thread, once a frame: the flight's values (the arm state; m, m, s).</summary>
        public void Flight(bool armed, double altitudeM, double distanceM, double flightSeconds)
        {
            _now.Armed = armed;
            _now.AltitudeM = altitudeM;
            _now.DistanceM = distanceM;
            _now.ElapsedSeconds = flightSeconds;
        }

        /// <summary>
        /// Main thread, once a frame: the field means of the latest whole 20 ms window of sim time, numbered (the
        /// physics': its Step / FlightCore.FieldSteps). Each new window runs Betaflight's battery tasks once; a frame that
        /// missed windows runs them for each with the latest means (the integral then assumes the current held).
        /// </summary>
        public void Sample(long window, double packVolts, double currentA)
        {
            if (!_now.Sampled)
            {
                _now.Sampled = true;
                _window = window;
                _volts = packVolts;
                _amps = currentA;
                _mAh = currentA * WindowS / 3.6; // the first window's charge too
                Alarm();
            }
            else if (window > _window)
            {
                long n = window - _window;
                _window = window;
                _mAh += currentA * n * WindowS / 3.6; // A·s to mAh
                for (long i = 0, m = Math.Min(n, SettleWindows); i < m; i++)
                {
                    _volts += VoltsGain * (packVolts - _volts);
                    _amps += AmpsGain * (currentA - _amps);
                    Alarm();
                }
            }
            _now.PackCentivolts = PackCentivolts;
            _now.CellCentivolts = Cells > 0 ? _now.PackCentivolts / Cells : 0;
            _now.Centiamps = (int)Math.Floor(Math.Abs(_amps) * 100);
            _now.MahDrawn = (int)_mAh;
            _now.LowBattery = _low;
        }

        int PackCentivolts => (int)Math.Round(_volts * 100);

        // Betaflight's batteryUpdateVoltageState, OK and WARNING (the sim publishes no critical level).
        void Alarm()
        {
            int warning = Cells * vbat_warning_cell_voltage, cv = PackCentivolts;
            if (!_low) _low = cv <= warning - vbat_hysteresis;
            else if (cv > warning) _low = false;
        }

        /// <summary>Lays the grid out for one video field: the values of the OSD's latest refresh, blinking applied.</summary>
        public void Build(int field, float linkMarginDb)
        {
            long refresh = (long)field * osd_framerate_hz / FieldHz;
            if (refresh != _refresh)
            {
                _refresh = refresh;
                _shown = _now;
            }
            Readout r = _shown;
            bool on = Blink(field);
            Clear();

            Put(RowTop, ColLeft, r.Armed ? ArmedMode : DisarmedMode);
            PutRight(RowTop, ColAltEnd, ">" + Math.Max(0, (int)Math.Round(r.AltitudeM)).ToString(Invariant) + "m");
            Put(RowReticle, ColReticle, "{|}");
            Put(RowArmed, ColLeft, Armament);

            int seconds = (int)r.ElapsedSeconds;
            Put(RowTimer, ColLeft, "[");
            Put(RowTimer, ColStack, (seconds / 60 % 100).ToString("00", Invariant) + ":" + (seconds % 60).ToString("00", Invariant));
            if (r.Sampled) PutRight(RowTimer, ColRightEnd, r.MahDrawn.ToString(Invariant).PadLeft(4) + MahUnit);

            // The voltages blink while the battery is not OK (osdUpdateAlarms), with the warning.
            if (r.Sampled && (!r.LowBattery || on))
            {
                Put(RowCell, ColLeft, "$");
                Put(RowCell, ColStack, Centi(r.CellCentivolts) + "в");
                Put(RowPack, ColLeft, "$");
                Put(RowPack, ColStack, PackText(r.PackCentivolts) + "в");
            }

            Put(RowCell, ColName, Airframe);
            PutRight(RowCell, ColRightEnd, (r.DistanceM / 1000).ToString("0.00", Invariant));
            if (r.Sampled)
            {
                PutRight(RowPack, ColAmpsEnd, (r.Centiamps / 100).ToString(Invariant).PadLeft(3) + "." + (r.Centiamps % 100).ToString("00", Invariant));
                Put(RowPack, ColAmpsEnd + 1, "a");
            }

            string warning = r.LowBattery ? LowBattery : FeedSignalScale.LinkMargin(linkMarginDb) < LowLinkFraction ? "LOW LINK" : null;
            if (warning != null && on)
            {
                for (int c = 0; c < Airframe.Length; c++)
                    Grid[RowCell, ColName + c] = ' ';
                Put(RowCell, ColName + (Airframe.Length - warning.Length) / 2, warning);
            }
        }

        /// <summary>The grid's row as text.</summary>
        public string Row(int row)
        {
            var chars = new char[FeedOsd.Cols];
            for (int c = 0; c < FeedOsd.Cols; c++) chars[c] = Grid[row, c];
            return new string(chars);
        }

        /// <summary>The pack's text, as osdElementMainBatteryVoltage prints it: one decimal from 10 V up, rounded half to
        /// even (lrintf), two below.</summary>
        public static string PackText(int centivolts)
        {
            if (centivolts < 1000) return Centi(centivolts);
            int tenths = (int)Math.Round(centivolts / 10.0, MidpointRounding.ToEven);
            return (tenths / 10).ToString(Invariant) + "." + (tenths % 10).ToString(Invariant);
        }

        static string Centi(int centi) => (centi / 100).ToString(Invariant) + "." + (centi % 100).ToString("00", Invariant);

        /// <summary>Betaflight's pt1FilterGain at its 50 Hz battery tasks: ω / (ω + 1), ω = 2π f dT.</summary>
        public static double Pt1Gain(double cutoffHz)
        {
            double omega = TwoPi * cutoffHz * WindowS;
            return omega / (omega + 1);
        }

        /// <summary>True while a blinking element is on.</summary>
        public static bool Blink(int field) => field % (BlinkOnFields + BlinkOffFields) < BlinkOnFields;

        void Clear()
        {
            for (int r = 0; r < FeedOsd.Rows; r++)
                for (int c = 0; c < FeedOsd.Cols; c++)
                    Grid[r, c] = ' ';
        }

        void Put(int row, int col, string text)
        {
            for (int i = 0; i < text.Length && col + i < FeedOsd.Cols; i++)
                if (col + i >= 0)
                    Grid[row, col + i] = text[i];
        }

        void PutRight(int row, int lastCol, string text) => Put(row, lastCol - text.Length + 1, text);
    }
}
