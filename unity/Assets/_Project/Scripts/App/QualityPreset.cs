using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Fpv450.Input.Radio;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// The quality preset (APP-05, D-052), which no screen offers (PRD-04, PRD-05). A player starts on the preset saved in
    /// the profile folder (quality.json, beside setup.json), or on the build's (High) when none is saved. The command
    /// line's <c>-quality &lt;name&gt;</c>, one of QualitySettings.names in any case, picks another and saves it for the next
    /// start. The editor keeps its own.
    /// </summary>
    public static class QualityPreset
    {
        public const string FileName = "quality.json";
        const string Format = "fpv450 quality 1";

        public static string PathIn(string directory) => Path.Combine(directory, FileName);

        /// <summary>At the player's start, before the first frame (it resets vSyncCount to the preset's): sets the preset,
        /// saves one the command line gave, and logs which it is.</summary>
        public static void Apply(string[] args, string directory)
        {
            string[] names = QualitySettings.names;
            string saved = Load(directory, out string error);
            if (error != null) Debug.LogWarning("[App] " + error);
            int level = Pick(args, names, saved, out bool given, out string problem);
            if (problem != null) Debug.LogWarning("[App] " + problem);
            if (level >= 0) QualitySettings.SetQualityLevel(level, true);
            if (given)
            {
                try
                {
                    Save(directory, names[level]);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[App] " + FileName + " could not be saved: " + e.Message);
                }
            }
            Debug.Log("[App] quality: " + names[QualitySettings.GetQualityLevel()] + (given ? " (-quality, saved for the next start)" : level >= 0 ? " (saved)" : " (the build's)"));
        }

        /// <summary>
        /// The preset's index in names: -quality's when it names one (ignoring case), and then given; otherwise the saved
        /// one's; otherwise -1, the build's. A -quality that names none is ignored, and problem says so.
        /// </summary>
        public static int Pick(string[] args, string[] names, string saved, out bool given, out string problem)
        {
            given = false;
            problem = null;
            int at = Array.IndexOf(args, "-quality");
            if (at >= 0)
            {
                string name = at + 1 < args.Length ? args[at + 1] : "";
                int level = Find(names, name);
                if (level >= 0)
                {
                    given = true;
                    return level;
                }
                problem = "-quality '" + name + "' is not one of " + string.Join(", ", names) + ": it is ignored.";
            }
            return Find(names, saved);
        }

        /// <summary>The saved preset's name, or null when none is saved or the file cannot be read (then error says why).</summary>
        public static string Load(string directory, out string error)
        {
            error = null;
            string path = PathIn(directory);
            if (!File.Exists(path)) return null;
            try
            {
                Dictionary<string, object> o = JsonRead.Object(MiniJson.Parse(File.ReadAllText(path, Encoding.UTF8)), "the file");
                if (JsonRead.Text(o, "format") != Format) throw new FormatException("not a " + Format + " file");
                return JsonRead.Text(o, "preset");
            }
            catch (Exception e)
            {
                error = FileName + " could not be read (" + e.Message + "): the build's preset is in use.";
                return null;
            }
        }

        /// <summary>Writes a temporary file and swaps it in, as the other stores do.</summary>
        public static void Save(string directory, string preset)
        {
            Directory.CreateDirectory(directory);
            string path = PathIn(directory), temp = path + ".tmp";
            string json = "{\n  \"format\": " + MiniJson.Quote(Format) + ",\n  \"preset\": " + MiniJson.Quote(preset) + "\n}\n";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        static int Find(string[] names, string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }
    }
}
