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
        /// The game (p6-input-app 1.4b): the build settings' scenes, Boot (the menu) and the farmland (the flight,
        /// w3-farmland-ground 3.1, and Explore's noclip, w1-farmland-world 2.1; it needs the vendor store linked,
        /// tools/vendor/link.ps1), full screen at the
        /// display's resolution, with copies of the files the flight reads (AppData.PlayerFiles) under its StreamingAssets
        /// at their repository paths. A scene with a missing reference fails the build (SceneReferences): the farmland in an
        /// unlinked checkout. Each map's top-down image is rendered first (TopDownMaps, w11-world-map 1.1), so the build
        /// needs graphics (never <c>-nographics</c>), and everything under <see cref="GeneratedFolder"/> goes into the
        /// player's <c>StreamingAssets/generated/</c>. <c>-review</c> makes a review build (FPV450_REVIEW: Explore and the
        /// flight's review keys) in <c>Builds/Windows-Review/</c>; a development build is a review build too.
        /// </summary>
        public static void Windows()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (!scenes.Contains(EditorSetup.ProjectSetup.FarmlandScenePath))
            {
                Debug.LogError($"[Build] Windows: the build settings lack {EditorSetup.ProjectSetup.FarmlandScenePath}; run Fpv450.EditorSetup.ProjectSetup.Apply");
                EditorApplication.Exit(1);
                return;
            }
            // w1-farmland-world 2.4: a scene with missing references would ship broken, as the farmland does in a checkout
            // without the vendor store's links.
            foreach (string scene in scenes)
            {
                var missing = SceneReferences.Missing(scene);
                Debug.Log($"[Build] Windows: {scene}: {missing.Count} missing references");
                if (missing.Count == 0) continue;
                Debug.LogError($"[Build] Windows: {scene} has {missing.Count} missing references, so the player would show it broken. " +
                               "The farmland's come from the vendor store: run pwsh tools/vendor/link.ps1 in this checkout, then build again " +
                               "(w1-farmland-world design 3). The first:\n  " + string.Join("\n  ", missing.Take(20)));
                EditorApplication.Exit(1);
                return;
            }
            if (!TopDownMaps.RenderAll())
            {
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
            return summary.result == BuildResult.Succeeded && summary.totalErrors == 0 && (!Development || LoopbackOnly(path));
        }

        /// <summary>
        /// A development player's connection for the profiler listens on every interface and announces itself by UDP
        /// multicast, which makes Windows Defender Firewall ask about each new player's path. Its boot.config binds the
        /// listener to loopback and turns the multicast off (UnityPlayer's player-connection-listen-address and
        /// player-connection-multicast), so the player opens no socket beyond this machine; a local profiler still reaches
        /// it at 127.0.0.1. A release player has no connection and is left alone.
        /// </summary>
        static bool LoopbackOnly(string path)
        {
            string boot = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "_Data", "boot.config");
            if (!File.Exists(boot))
            {
                Debug.LogError($"[Build] the development player's {boot} is missing");
                return false;
            }
            var lines = File.ReadAllLines(boot).Where(l => !l.StartsWith("player-connection-listen-address=") && !l.StartsWith("player-connection-multicast=")).ToList();
            lines.Add("player-connection-listen-address=127.0.0.1");
            lines.Add("player-connection-multicast=0");
            File.WriteAllLines(boot, lines);
            Debug.Log($"[Build] the development player's connection: loopback only, no multicast ({boot})");
            return true;
        }

        /// <summary>The player's own copies of the flight's files (AppData.PlayerFiles), under its StreamingAssets at their
        /// repository paths, so AppData.Root finds them as the editor finds the repository's.</summary>
        static bool CopyData(string path)
        {
            path = Located(path);
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string streaming = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "_Data", "StreamingAssets");
            foreach (string file in Fpv450.App.AppData.PlayerFiles(root))
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
            if (Directory.Exists(GeneratedFolder))
                foreach (string from in Directory.GetFiles(GeneratedFolder, "*", SearchOption.AllDirectories))
                {
                    string file = Path.GetRelativePath(GeneratedFolder, from), to = Path.Combine(streaming, "generated", file);
                    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                    File.Copy(from, to, true);
                    Debug.Log($"[Build] the player's generated files: generated/{file.Replace('\\', '/')} ({new FileInfo(to).Length} bytes)");
                }
            return true;
        }

        /// <summary>
        /// What the build renders for the player (<c>unity/Generated/&lt;kind&gt;/</c>: <c>topdown/</c>, the maps' top-down
        /// images, w11-world-map; <c>stills/</c>, the maps' stills, w10-play-maps), git-ignored and never pushed (D-089). The
        /// build carries it into the player's <c>StreamingAssets/generated/&lt;kind&gt;/</c>.
        /// </summary>
        public static string GeneratedFolder => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Generated");
    }
}
