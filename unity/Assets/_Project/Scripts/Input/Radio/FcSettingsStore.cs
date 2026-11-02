using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Fpv450.Sim.Control;

namespace Fpv450.Input.Radio
{
    /// <summary>
    /// One setting the Calibrate screen edits (INP-03): the FcSettings field of its Betaflight CLI name, shown in the
    /// configurator's units (Shown = CLI value × Mul / Div), stepped as the screen steps it, and checked against
    /// Betaflight 4.5's own range when a file is read. Curve names the screen's preview: 0–2 a rate axis, 3 the throttle.
    /// </summary>
    public sealed class FcSettingRow
    {
        public const int ThrottleCurve = 3;

        public readonly string Name, Group, Label, Format, Unit;
        public readonly int Curve;
        public readonly double Mul, Div;

        /// <summary>CLI units: the screen's step, Betaflight's range, and the slider's range (the screen's, assumed).</summary>
        public readonly double Step, Low, High, SliderLow, SliderHigh;

        /// <summary>Betaflight keeps it a whole number (a uint8 setting).</summary>
        public readonly bool Whole;

        readonly FieldInfo _field;

        FcSettingRow(string name, string group, int curve, string label, string format, string unit, double mul, double div,
            double step, double low, double high, double sliderLow, double sliderHigh, bool whole)
        {
            Name = name; Group = group; Curve = curve; Label = label; Format = format; Unit = unit; Mul = mul; Div = div;
            Step = step; Low = low; High = high; SliderLow = sliderLow; SliderHigh = sliderHigh; Whole = whole;
            _field = FcSettingsStore.Field(name) ?? throw new ArgumentException("FcSettings has no field " + name);
        }

        public double Get(in FcSettings s) => (double)_field.GetValue(s);

        public void Set(ref FcSettings s, double cli)
        {
            object box = s;
            _field.SetValue(box, cli);
            s = (FcSettings)box;
        }

        public double Shown(in FcSettings s) => Get(s) * Mul / Div;

        /// <summary>A CLI value on the screen's step, within Betaflight's range.</summary>
        public double Snap(double cli)
        {
            double v = Math.Round(cli / Step) * Step;
            return v < Low ? Low : v > High ? High : v;
        }

        /// <summary>Null when cli is a value Betaflight takes for this setting, otherwise why not.</summary>
        public string Check(double cli)
        {
            if (!(cli >= Low && cli <= High)) return "'" + Name + "' " + N(cli) + " is outside Betaflight's " + N(Low) + "–" + N(High);
            if (Whole && cli != Math.Floor(cli)) return "'" + Name + "' " + N(cli) + " is not a whole number";
            return null;
        }

        static string N(double v) => v.ToString(CultureInfo.InvariantCulture);

        // ACTUAL rates in tens of °/s: the centre sensitivity and the full-stick rate on 5 °/s steps (the sim default
        // column's halves); Betaflight's range is controlrate_profile.c's ACTUAL limits (rc_rate and srate 200, expo
        // 100) over settings.c's minimums (1, 0, 0). The sliders' 10–250 and 50–1000 °/s are the screen's (assumed).
        static FcSettingRow Centre(string name, string group, int axis) =>
            new FcSettingRow(name, group, axis, "Centre", "0", " °/s", 10, 1, 0.5, 1, 200, 1, 25, false);

        static FcSettingRow MaxRate(string name, string group, int axis) =>
            new FcSettingRow(name, group, axis, "Max rate", "0", " °/s", 10, 1, 0.5, 0, 200, 5, 100, false);

        // Expo, thr_mid and thr_expo: 0–100 whole in the CLI, shown 0.00–1.00 as the configurator shows them.
        static FcSettingRow Hundredths(string name, string group, int curve, string label) =>
            new FcSettingRow(name, group, curve, label, "0.00", "", 1, 100, 1, 0, 100, 0, 100, true);

        /// <summary>The screen's settings in its order, a column a group. A new Betaflight setting is a new row.</summary>
        public static readonly FcSettingRow[] Rows =
        {
            Centre("roll_rc_rate", "Roll", 0), MaxRate("roll_srate", "Roll", 0), Hundredths("roll_expo", "Roll", 0, "Expo"),
            Centre("pitch_rc_rate", "Pitch", 1), MaxRate("pitch_srate", "Pitch", 1), Hundredths("pitch_expo", "Pitch", 1, "Expo"),
            Centre("yaw_rc_rate", "Yaw", 2), MaxRate("yaw_srate", "Yaw", 2), Hundredths("yaw_expo", "Yaw", 2, "Expo"),
            Hundredths("thr_mid", "Throttle", ThrottleCurve, "Mid"), Hundredths("thr_expo", "Throttle", ThrottleCurve, "Expo"),
        };
    }

    /// <summary>
    /// The pilot's flight-controller settings (INP-03, INP-07, APP-05): fc-settings.json in the user's profile folder
    /// (Unity's persistentDataPath), beside calibration.json, as Betaflight CLI names and CLI values. With no file the
    /// settings are PlayerDefault. The file holds every row the screen edits and any
    /// other FcSettings field that is not its default, so a setting the screen does not show yet survives a save and an
    /// imported `diff all` can write the same form. Reading takes any FcSettings field by its CLI name, checks the rows
    /// against Betaflight's ranges and the whole set against the flight controller, and starts from the defaults for
    /// every name the file leaves out. Saving writes a temporary file and then swaps it in, as CalibrationStore does.
    /// </summary>
    public static class FcSettingsStore
    {
        public const string FileName = "fc-settings.json";
        const string Format = "fpv450 fc settings 1";

        /// <summary>
        /// The player's defaults (D-066, D-023; fc-profiles.md §2, the player default column): FcSettings.Default with
        /// the screen's rows from the pilot's FpvPrep 10-inch profile, its rates as saved and its throttle curve as the
        /// builder ships it, without FpvPrep's "throttle softer" steps. The sim core's FcSettings.Default stays the
        /// controller of its tests and golden flights.
        /// </summary>
        public static FcSettings PlayerDefault
        {
            get
            {
                FcSettings s = FcSettings.Default;
                s.roll_rc_rate = s.pitch_rc_rate = 5; s.roll_srate = s.pitch_srate = 22; s.roll_expo = s.pitch_expo = 45;
                s.yaw_rc_rate = 8; s.yaw_srate = 24; s.yaw_expo = 20;
                s.thr_mid = 100; s.thr_expo = 100;
                return s;
            }
        }

        static Dictionary<string, FieldInfo> _fields;

        /// <summary>The FcSettings field of a CLI name, or null.</summary>
        public static FieldInfo Field(string name)
        {
            if (_fields == null)
            {
                var fields = new Dictionary<string, FieldInfo>();
                foreach (FieldInfo f in typeof(FcSettings).GetFields(BindingFlags.Public | BindingFlags.Instance)) fields[f.Name] = f;
                _fields = fields;
            }
            return _fields.TryGetValue(name, out FieldInfo field) ? field : null;
        }

        public static string PathIn(string directory) => Path.Combine(directory, FileName);

        /// <summary>The settings in directory: the defaults when there is no file yet, or when the file cannot be read,
        /// with the reason in error (null otherwise).</summary>
        public static FcSettings Load(string directory, out string error)
        {
            error = null;
            string path = PathIn(directory);
            if (!File.Exists(path)) return PlayerDefault;
            try
            {
                return FromJson(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e)
            {
                error = FileName + " could not be read (" + e.Message + "): the defaults are in use, and the next change replaces it.";
                return PlayerDefault;
            }
        }

        public static void Save(string directory, in FcSettings settings)
        {
            Directory.CreateDirectory(directory);
            string path = PathIn(directory), temp = path + ".tmp";
            File.WriteAllText(temp, ToJson(settings), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        public static string ToJson(in FcSettings settings)
        {
            var b = new StringBuilder();
            b.Append("{\n  \"format\": ").Append(MiniJson.Quote(Format)).Append(",\n  \"settings\": {");
            var written = new HashSet<string>();
            bool first = true;
            foreach (FcSettingRow row in FcSettingRow.Rows)
            {
                Member(b, ref first, row.Name, row.Get(settings));
                written.Add(row.Name);
            }
            object value = settings, defaults = PlayerDefault;
            foreach (FieldInfo f in typeof(FcSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v = f.GetValue(value);
                if (written.Contains(f.Name) || Same(v, f.GetValue(defaults))) continue;
                Member(b, ref first, f.Name, v);
            }
            return b.Append("\n  }\n}\n").ToString();
        }

        public static FcSettings FromJson(string json)
        {
            Dictionary<string, object> root = JsonRead.Object(MiniJson.Parse(json), FileName);
            if (JsonRead.Text(root, "format") != Format) throw new FormatException(FileName + " is not " + Format);
            Dictionary<string, object> set = JsonRead.Object(JsonRead.Member(root, "settings"), "'settings'");
            object box = PlayerDefault;
            foreach (KeyValuePair<string, object> kv in set)
            {
                FieldInfo f = Field(kv.Key) ?? throw new FormatException("no Betaflight setting '" + kv.Key + "' here");
                f.SetValue(box, Value(f, kv.Key, kv.Value));
            }
            var settings = (FcSettings)box;
            foreach (FcSettingRow row in FcSettingRow.Rows)
            {
                string problem = row.Check(row.Get(settings));
                if (problem != null) throw new FormatException(problem);
            }
            try
            {
                new FlightController(settings, RatePreview.AnySpins);
            }
            catch (ArgumentException e)
            {
                throw new FormatException("the flight controller refuses them: " + e.Message);
            }
            return settings;
        }

        static object Value(FieldInfo f, string key, object v)
        {
            if (f.FieldType == typeof(double))
                return v is double d ? d : throw new FormatException("'" + key + "' is not a number");
            if (f.FieldType == typeof(bool))
                return v is bool b ? b : throw new FormatException("'" + key + "' is not true or false");
            if (f.FieldType.IsEnum && v is string t && Enum.IsDefined(f.FieldType, t)) return Enum.Parse(f.FieldType, t);
            throw new FormatException("'" + key + "' is not one of " + string.Join(", ", Enum.GetNames(f.FieldType)));
        }

        static void Member(StringBuilder b, ref bool first, string name, object v)
        {
            b.Append(first ? "\n" : ",\n").Append("    ").Append(MiniJson.Quote(name)).Append(": ");
            first = false;
            if (v is double d) b.Append(Number(d));
            else if (v is bool on) b.Append(on ? "true" : "false");
            else b.Append(MiniJson.Quote(v.ToString()));
        }

        // The shortest text that reads back to the same double; G17 where a runtime's "R" falls short.
        static string Number(double v)
        {
            string t = v.ToString("R", CultureInfo.InvariantCulture);
            return double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture) == v ? t : v.ToString("G17", CultureInfo.InvariantCulture);
        }

        static bool Same(object a, object b) =>
            a is double x && b is double y ? BitConverter.DoubleToInt64Bits(x) == BitConverter.DoubleToInt64Bits(y) : a.Equals(b);
    }
}
