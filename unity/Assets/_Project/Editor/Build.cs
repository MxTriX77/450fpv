using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Fpv450
{
    /// <summary>
    /// Batchmode build entry points, run as <c>-executeMethod Fpv450.Build.Windows</c>, <c>Fpv450.Build.Gallery</c> or
    /// <c>Fpv450.Build.TestRange</c>.
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
        public static void Gallery() =>
            OneScene("Gallery", "450fpv gallery", GalleryScene.ScenePath, "Builds/Windows-Gallery/450fpv-gallery.exe");

        /// <summary>The test range (p6-test-range design 9): its scene alone, as the gallery's build, under its own product name.</summary>
        public static void TestRange() =>
            OneScene("TestRange", "450fpv test range", TestRangeScene.ScenePath, "Builds/Windows-TestRange/450fpv-test-range.exe");

        static void OneScene(string name, string productName, string scene, string path)
        {
            string product = PlayerSettings.productName;
            FullScreenMode mode = PlayerSettings.fullScreenMode;
            bool native = PlayerSettings.defaultIsNativeResolution;
            bool built;
            try
            {
                PlayerSettings.productName = productName;
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                PlayerSettings.defaultIsNativeResolution = true;
                built = Player(name, new[] { scene }, path);
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

        /// <c>-developmentBuild</c> makes a development player instead, for the profiler's GPU timings (PLT-07, the
        /// feed's <c>-feedcost</c>), in its own folder: the release folder's name with "-Development".
        static readonly bool Development = Array.IndexOf(Environment.GetCommandLineArgs(), "-developmentBuild") >= 0;

        static bool Player(string name, string[] scenes, string path)
        {
            if (Development)
                path = Path.Combine(Path.GetDirectoryName(path) + "-Development", Path.GetFileName(path));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = Development ? BuildOptions.Development : BuildOptions.None,
            });

            BuildSummary summary = report.summary;
            Debug.Log($"[Build] {name}: {summary.result}, {summary.totalErrors} errors, {summary.totalWarnings} warnings, " +
                      $"{summary.totalSize} bytes in {summary.totalTime}");
            return summary.result == BuildResult.Succeeded && summary.totalErrors == 0;
        }
    }
}
