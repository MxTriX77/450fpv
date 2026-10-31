using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Fpv450.Input.Radio
{
    /// <summary>
    /// The pilot's named calibration profiles (INP-02, APP-05): one JSON file, calibration.json, in the user's profile
    /// folder (Unity's persistentDataPath), with the active profile's name. A profile saved under a name it already has
    /// replaces it. Saving writes a temporary file and then swaps it in, so a crash never leaves half a file.
    /// </summary>
    public sealed class CalibrationStore
    {
        public const string FileName = "calibration.json";
        const string Format = "fpv450 calibration 1";

        public CalibrationStore(string directory) => FilePath = Path.Combine(directory, FileName);

        public string FilePath { get; }

        public List<CalibrationProfile> Profiles { get; } = new List<CalibrationProfile>();

        /// <summary>The active profile's name, or "" for none.</summary>
        public string Active = "";

        public CalibrationProfile Find(string name) => Profiles.Find(p => p.Name == name);

        public CalibrationProfile ActiveProfile => Find(Active);

        /// <summary>Adds the profile (a copy), replacing one of its name, and makes it the active one.</summary>
        public void Put(CalibrationProfile profile)
        {
            Profiles.RemoveAll(p => p.Name == profile.Name);
            Profiles.Add(profile.Clone());
            Active = profile.Name;
        }

        /// <summary>The store in directory; empty when it has no file yet. A file that is not one throws.</summary>
        public static CalibrationStore Load(string directory)
        {
            var store = new CalibrationStore(directory);
            if (File.Exists(store.FilePath)) store.Read(File.ReadAllText(store.FilePath, Encoding.UTF8));
            return store;
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, ToJson(), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }

        public string ToJson()
        {
            var b = new StringBuilder();
            b.Append("{\n  \"format\": ").Append(MiniJson.Quote(Format)).Append(",\n");
            b.Append("  \"active\": ").Append(MiniJson.Quote(Active)).Append(",\n");
            b.Append("  \"profiles\": [");
            for (int i = 0; i < Profiles.Count; i++)
            {
                CalibrationProfile p = Profiles[i];
                b.Append(i == 0 ? "\n" : ",\n");
                b.Append("    {\n");
                b.Append("      \"name\": ").Append(MiniJson.Quote(p.Name)).Append(",\n");
                b.Append("      \"device\": ").Append(MiniJson.Quote(p.Device)).Append(",\n");
                b.Append("      \"order\": ").Append(MiniJson.Quote(p.Order)).Append(",\n");
                Stick(b, "roll", p.Roll);
                Stick(b, "pitch", p.Pitch);
                Stick(b, "yaw", p.Yaw);
                Stick(b, "throttle", p.Throttle);
                ArmCalibration a = p.Arm;
                b.Append("      \"arm\": { \"control\": ").Append(MiniJson.Quote(a.Control.ToString().ToLowerInvariant()))
                    .Append(", \"index\": ").Append(I(a.Index)).Append(", \"threshold\": ").Append(I(a.Threshold))
                    .Append(", \"on_high\": ").Append(a.OnHigh ? "true" : "false").Append(" }\n");
                b.Append("    }");
            }
            b.Append(Profiles.Count > 0 ? "\n  ]\n}\n" : "]\n}\n");
            return b.ToString();
        }

        static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        static void Stick(StringBuilder b, string key, StickCalibration c) =>
            b.Append("      \"").Append(key).Append("\": { \"axis\": ").Append(I(c.Axis))
                .Append(", \"reversed\": ").Append(c.Reversed ? "true" : "false")
                .Append(", \"min\": ").Append(I(c.Min)).Append(", \"centre\": ").Append(I(c.Centre)).Append(", \"max\": ").Append(I(c.Max))
                .Append(", \"deadband\": ").Append(I(c.Deadband)).Append(" },\n");

        void Read(string json)
        {
            Dictionary<string, object> root = JsonRead.Object(MiniJson.Parse(json), FileName);
            if (JsonRead.Text(root, "format") != Format) throw new FormatException(FileName + " is not " + Format);
            Active = JsonRead.Text(root, "active");
            if (!(JsonRead.Member(root, "profiles") is List<object> list)) throw new FormatException("'profiles' is not a list");
            foreach (object item in list)
            {
                Dictionary<string, object> o = JsonRead.Object(item, "a profile");
                Dictionary<string, object> arm = JsonRead.Object(JsonRead.Member(o, "arm"), "'arm'");
                Profiles.Add(new CalibrationProfile
                {
                    Name = JsonRead.Text(o, "name"),
                    Device = JsonRead.Text(o, "device"),
                    Roll = Stick(o, "roll"),
                    Pitch = Stick(o, "pitch"),
                    Yaw = Stick(o, "yaw"),
                    Throttle = Stick(o, "throttle"),
                    Arm = new ArmCalibration
                    {
                        Control = (ArmControl)Enum.Parse(typeof(ArmControl), JsonRead.Text(arm, "control"), true),
                        Index = JsonRead.Int(arm, "index"),
                        Threshold = JsonRead.Int(arm, "threshold"),
                        OnHigh = JsonRead.Bool(arm, "on_high"),
                    },
                });
            }
        }

        static StickCalibration Stick(Dictionary<string, object> profile, string key)
        {
            Dictionary<string, object> o = JsonRead.Object(JsonRead.Member(profile, key), "'" + key + "'");
            return new StickCalibration
            {
                Axis = JsonRead.Int(o, "axis"),
                Reversed = JsonRead.Bool(o, "reversed"),
                Min = JsonRead.Int(o, "min"),
                Centre = JsonRead.Int(o, "centre"),
                Max = JsonRead.Int(o, "max"),
                Deadband = JsonRead.Int(o, "deadband"),
            };
        }
    }
}
