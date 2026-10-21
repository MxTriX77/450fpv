using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Fpv450
{
    /// <summary>
    /// Batchmode build entry points, run as <c>-executeMethod Fpv450.Build.Windows</c> or <c>Fpv450.Build.Gallery</c>.
    /// A failed build exits Unity with code 1.
    /// </summary>
    public static class Build
    {
        public static void Windows()
        {
            if (!Player("Windows", EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(), "Builds/Windows/450fpv.exe"))
                EditorApplication.Exit(1);
        }

        /// <summary>
        /// The G1 gallery (p1-g1-look-lock-gallery design 5 and 6): its scene alone, full screen at the display's
        /// resolution by default (task 1.3: a 1080p window does not fit a 1080p display with its title bar;
        /// <c>GalleryBoot</c> gives <c>-windowed</c>), under its own product name (so its window settings are its own).
        /// The project's settings are restored after.
        /// </summary>
        public static void Gallery()
        {
            string product = PlayerSettings.productName;
            FullScreenMode mode = PlayerSettings.fullScreenMode;
            bool native = PlayerSettings.defaultIsNativeResolution;
            bool built;
            try
            {
                PlayerSettings.productName = "450fpv gallery";
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                PlayerSettings.defaultIsNativeResolution = true;
                built = Player("Gallery", new[] { GalleryScene.ScenePath }, "Builds/Windows-Gallery/450fpv-gallery.exe");
            }
            finally
            {
                PlayerSettings.productName = product;
                PlayerSettings.fullScreenMode = mode;
                PlayerSettings.defaultIsNativeResolution = native;
            }
            if (!built)
                EditorApplication.Exit(1);
        }

        static bool Player(string name, string[] scenes, string path)
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            });

            BuildSummary summary = report.summary;
            Debug.Log($"[Build] {name}: {summary.result}, {summary.totalErrors} errors, {summary.totalWarnings} warnings, " +
                      $"{summary.totalSize} bytes in {summary.totalTime}");
            return summary.result == BuildResult.Succeeded && summary.totalErrors == 0;
        }
    }
}
