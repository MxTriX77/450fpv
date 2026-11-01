using System.IO;
using Fpv450.App.Session;
using Fpv450.Sim.Contact;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// Where the app's data files are, and what kind of build this is. The flight reads drone.json, the physics materials
    /// and the test range's placements by their repository paths, under a root: the repository in the editor, and in a
    /// player its StreamingAssets folder, which <c>Fpv450.Build</c> fills with copies at the same paths (1.4a's note).
    /// </summary>
    public static class AppData
    {
        public const string DroneFile = "assets-src/drone/drone.json";

        /// <summary>The files a player carries, by their paths under the root.</summary>
        public static readonly string[] PlayerFiles =
        {
            DroneFile,
            PhysicsMaterials.RelativePath,
            "maps/" + FlightWorlds.TestRangeMap + "/placements.json",
        };

        public static string Root => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."))
            : Application.streamingAssetsPath;

        public static string File(string relative) => Path.Combine(Root, relative);

        /// <summary>
        /// A review build (PRD-05): the menu offers Explore, and the flight has its review keys (C the chase view, V the
        /// feed). <c>Fpv450.Build</c> makes one with <c>-review</c>; every development build and the editor are one too.
        /// </summary>
        public static bool Review =>
#if FPV450_REVIEW
            true;
#else
            Debug.isDebugBuild;
#endif
    }
}
