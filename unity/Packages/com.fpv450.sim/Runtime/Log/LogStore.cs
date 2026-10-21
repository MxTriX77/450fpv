using System;
using System.Globalization;
using System.IO;

namespace Fpv450.Sim.Log
{
    /// <summary>
    /// Where flight logs live (LOG-05): under the user's profile, in the local application data folder
    /// (%LOCALAPPDATA%\450fpv\flights on Windows), never beside the game or in the repository. A log holds sim data
    /// only, and its name holds only the flight's seed and a count: no date, user or place. Read at a flight's start,
    /// never by a step.
    /// </summary>
    public static class LogStore
    {
        public const string Extension = ".flightlog";

        /// <summary>The logs' folder under the user's profile.</summary>
        public static string Folder
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("this host has no local application data folder");
                return Path.Combine(root, "450fpv", "flights");
            }
        }

        /// <summary>A new log's path for a flight of seed: flight-SEED-N.flightlog, the first N not yet taken. Creates the folder.</summary>
        public static string NewPath(ulong seed, string folder = null)
        {
            folder = folder ?? Folder;
            Directory.CreateDirectory(folder);
            string stem = "flight-" + seed.ToString("x16", CultureInfo.InvariantCulture) + "-";
            for (int n = 1; ; n++)
            {
                string path = Path.Combine(folder, stem + n.ToString(CultureInfo.InvariantCulture) + Extension);
                if (!File.Exists(path)) return path;
            }
        }
    }
}
