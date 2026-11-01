using System.Collections.Generic;
using System.IO;
using Fpv450.App.Session;
using Fpv450.Sim.Contact;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// Where the app's data files are, and what kind of build this is. The flight reads drone.json, the physics materials
    /// and the test range's map (MapWorld's files) by their repository paths, under a root: the repository in the editor, and in a
    /// player its StreamingAssets folder, which <c>Fpv450.Build</c> fills with copies at the same paths (1.4a's note).
    /// </summary>
    public static class AppData
    {
        public const string DroneFile = "assets-src/drone/drone.json";

        /// <summary>The files a player carries, by their paths under the root: drone.json, the physics materials, and what
        /// MapWorld reads for the test range (its placements and each placed variant's metadata).</summary>
        public static List<string> PlayerFiles(string root)
        {
            var files = new List<string> { DroneFile, PhysicsMaterials.RelativePath };
            foreach (string file in FlightWorlds.TestRangeFiles(root))
                if (!files.Contains(file)) files.Add(file);
            return files;
        }

        public static string Root => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."))
            : Application.streamingAssetsPath;

        public static string File(string relative) => Path.Combine(Root, relative);

        /// <summary>
        /// A review build (PRD-05): the menu offers Explore, and the flight has its review key (F the feed off and on;
        /// V's view is every build's, D-055). <c>Fpv450.Build</c> makes one with <c>-review</c>; every development build and
        /// the editor are one too.
        /// </summary>
        public static bool Review =>
#if FPV450_REVIEW
            true;
#else
            Debug.isDebugBuild;
#endif
    }
}
