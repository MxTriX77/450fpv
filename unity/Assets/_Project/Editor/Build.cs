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
        /// <summary>
        /// The game (p6-input-app 1.4b): the build settings' scenes, Boot (the menu) and the test range, full screen at the
        /// display's resolution, with copies of the files the flight reads (AppData.PlayerFiles) under its StreamingAssets
        /// at their repository paths. <c>-review</c> makes a review build (FPV450_REVIEW: Explore and the flight's review
        /// keys) in <c>Builds/Windows-Review/</c>; a development build is a review build too.
        /// </summary>
        public static void Windows()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (!scenes.Contains(TestRangeScene.ScenePath))
            {
                Debug.LogError($"[Build] Windows: the build settings lack {TestRangeScene.ScenePath}; run Fpv450.EditorSetup.ProjectSetup.Apply");
                EditorApplication.Exit(1);
                return;
            }
            OneScene("Windows", PlayerSettings.productName, scenes, Review ? "Builds/Windows-Review/450fpv.exe" : "Builds/Windows/450fpv.exe", data: true);
        }

        static readonly bool Review = Array.IndexOf(Environment.GetCommandLineArgs(), "-review") >= 0;

        /// <summary>
        /// The G1 gallery (p1-g1-look-lock-gallery design 5 and 6): its scene alone, full screen at the display's
        /// resolution by default (task 1.3: a 1080p window does not fit a 1080p display with its title bar;
        /// <c>GalleryBoot</c> gives <c>-windowed</c>), under its own product name (so its window settings are its own).
        /// The project's settings are restored after.
        /// </summary>
        public static void Gallery() =>
            OneScene("Gallery", "450fpv gallery", new[] { GalleryScene.ScenePath }, "Builds/Windows-Gallery/450fpv-gallery.exe");

        /// <summary>The test range (p6-test-range design 9): its scene alone, as the gallery's build, under its own product name.</summary>
        public static void TestRange() =>
            OneScene("TestRange", "450fpv test range", new[] { TestRangeScene.ScenePath }, "Builds/Windows-TestRange/450fpv-test-range.exe");

        static void OneScene(string name, string productName, string[] scenes, string path, bool data = false)
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
                built = Player(name, scenes, path) && (!data || CopyData(path));
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

        static string Located(string path) =>
            Development ? Path.Combine(Path.GetDirectoryName(path) + "-Development", Path.GetFileName(path)) : path;

        static bool Player(string name, string[] scenes, string path)
        {
            path = Located(path);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = Development ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = Review ? new[] { "FPV450_REVIEW" } : null,
            });

            BuildSummary summary = report.summary;
            Debug.Log($"[Build] {name}: {summary.result}, {summary.totalErrors} errors, {summary.totalWarnings} warnings, " +
                      $"{summary.totalSize} bytes in {summary.totalTime}");
            return summary.result == BuildResult.Succeeded && summary.totalErrors == 0;
        }

        /// <summary>The player's own copies of the flight's files (AppData.PlayerFiles), under its StreamingAssets at their
        /// repository paths, so AppData.Root finds them as the editor finds the repository's.</summary>
        static bool CopyData(string path)
        {
            path = Located(path);
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string streaming = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "_Data", "StreamingAssets");
            foreach (string file in Fpv450.App.AppData.PlayerFiles)
            {
                string from = Path.Combine(root, file), to = Path.Combine(streaming, file);
                if (!File.Exists(from))
                {
                    Debug.LogError($"[Build] the player's data: {from} is missing");
                    return false;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(from, to, true);
                Debug.Log($"[Build] the player's data: {file} ({new FileInfo(to).Length} bytes)");
            }
            return true;
        }
    }
}
