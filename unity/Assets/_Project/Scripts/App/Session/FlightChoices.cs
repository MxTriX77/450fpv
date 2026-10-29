using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Fpv450.Input.Radio;
using Fpv450.Sim.Wind;

namespace Fpv450.App.Session
{
    /// <summary>The setup's rain (APP-03). The look's (p4-feed, the weather); the G7 physics has no rain.</summary>
    public enum Rain { Sunny, Rain, SevereRain }

    /// <summary>
    /// The setup screen's choices (APP-03), and nothing else: the payload, the legs (off: the launch stand), the time of
    /// day, the wind, the rain and the start point. The physics takes the payload, the legs, the wind and the start
    /// point; the time of day and the rain are the scene's (the sky, the weather's look).
    /// </summary>
    public sealed class FlightChoices
    {
        public const double PayloadMax = 2.0, PayloadStep = 0.1;

        /// <summary>kg, 0–2.0 in 0.1 kg steps.</summary>
        public double Payload = 1.0;

        public bool Legs = true;

        /// <summary>Hours, 0 to under 24, continuous: night included.</summary>
        public double TimeOfDay = 12;

        /// <summary>Calm, Windy or Severe (the stand-in wind's levels).</summary>
        public WindLevel Wind = WindLevel.Calm;

        public Rain Rain = Rain.Sunny;

        /// <summary>The start point's name in the world's list; empty for the world's first.</summary>
        public string StartPoint = "";

        public FlightChoices Clone() => (FlightChoices)MemberwiseClone();

        /// <summary>Why these choices cannot fly, or null.</summary>
        public string Check()
        {
            if (!(Payload >= 0 && Payload <= PayloadMax)) return "the payload is outside 0–2.0 kg";
            if (Math.Abs(Payload / PayloadStep - Math.Round(Payload / PayloadStep)) > 1e-9) return "the payload is not in 0.1 kg steps";
            if (!(TimeOfDay >= 0 && TimeOfDay < 24)) return "the time of day is outside 0–24 h";
            if (Wind != WindLevel.Calm && Wind != WindLevel.Windy && Wind != WindLevel.Severe) return "the wind is not Calm, Windy or Severe";
            if (Rain != Rain.Sunny && Rain != Rain.Rain && Rain != Rain.SevereRain) return "the rain is not Sunny, Rain or Severe rain";
            return null;
        }
    }

    /// <summary>
    /// The last setup (APP-03, APP-05): setup.json beside calibration.json and fc-settings.json in the user's profile
    /// folder. Saving writes a temporary file and swaps it in, as the other stores do. A file that cannot be read gives
    /// the defaults and the reason, and stays until the next save.
    /// </summary>
    public static class SetupStore
    {
        public const string FileName = "setup.json";
        const string Format = "fpv450 setup 1";

        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        public static string PathIn(string directory) => Path.Combine(directory, FileName);

        public static FlightChoices Load(string directory, out string error)
        {
            error = null;
            string path = PathIn(directory);
            if (!File.Exists(path)) return new FlightChoices();
            try
            {
                return FromJson(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e)
            {
                error = FileName + " could not be read (" + e.Message + "): the default setup is in use, and the next flight replaces it.";
                return new FlightChoices();
            }
        }

        public static void Save(string directory, FlightChoices choices)
        {
            Directory.CreateDirectory(directory);
            string path = PathIn(directory), temp = path + ".tmp";
            File.WriteAllText(temp, ToJson(choices), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        public static string ToJson(FlightChoices c)
        {
            var b = new StringBuilder();
            b.Append("{\n  \"format\": ").Append(MiniJson.Quote(Format));
            b.Append(",\n  \"payload_kg\": ").Append(c.Payload.ToString("R", C));
            b.Append(",\n  \"legs\": ").Append(c.Legs ? "true" : "false");
            b.Append(",\n  \"time_of_day_h\": ").Append(c.TimeOfDay.ToString("R", C));
            b.Append(",\n  \"wind\": ").Append(MiniJson.Quote(c.Wind.ToString()));
            b.Append(",\n  \"rain\": ").Append(MiniJson.Quote(c.Rain.ToString()));
            b.Append(",\n  \"start_point\": ").Append(MiniJson.Quote(c.StartPoint ?? ""));
            return b.Append("\n}\n").ToString();
        }

        public static FlightChoices FromJson(string json)
        {
            Dictionary<string, object> o = JsonRead.Object(MiniJson.Parse(json), "the file");
            if (JsonRead.Text(o, "format") != Format) throw new FormatException("not a " + Format + " file");
            var c = new FlightChoices
            {
                Payload = Number(o, "payload_kg"),
                Legs = JsonRead.Bool(o, "legs"),
                TimeOfDay = Number(o, "time_of_day_h"),
                Wind = Named<WindLevel>(o, "wind"),
                Rain = Named<Rain>(o, "rain"),
                StartPoint = JsonRead.Text(o, "start_point"),
            };
            string problem = c.Check();
            if (problem != null) throw new FormatException(problem);
            return c;
        }

        static double Number(Dictionary<string, object> o, string key) =>
            JsonRead.Member(o, key) is double d ? d : throw new FormatException("'" + key + "' is not a number");

        static T Named<T>(Dictionary<string, object> o, string key) where T : struct
        {
            string text = JsonRead.Text(o, key);
            if (text.Length > 0 && !char.IsDigit(text[0]) && Enum.TryParse(text, false, out T value) && Enum.IsDefined(typeof(T), value)) return value;
            throw new FormatException("'" + key + "' is not one of " + string.Join(", ", Enum.GetNames(typeof(T))));
        }
    }
}
