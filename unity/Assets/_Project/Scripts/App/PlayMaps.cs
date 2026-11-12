using System;
using System.IO;
using Fpv450.App.Session;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>A map Play offers (D-100): its map id, its name and line on the card, the scene its flights load, the world
    /// they fly (FlightWorlds, from the data root) and the start points its setup offers.</summary>
    public sealed class PlayMap
    {
        public string Id, Name, Line, Scene;
        public Func<string, FlightWorldChoice> World;
        public Func<string, StartPoint[]> Starts;
    }

    /// <summary>
    /// The maps Play offers (w10-play-maps 1.1, D-100), in the carousel's order: House, the test range (D-044, D-047), from
    /// its houses' start (D-077), and Village, the farmland (D-088), from its village's start (D-092). FlightSessionHost
    /// flies the map of the scene it is in; each card shows the map's still when the build carries one (task 1.2).
    /// </summary>
    public static class PlayMaps
    {
        public const string TestRangeScene = "TestRange";

        public static readonly PlayMap House = new PlayMap
        {
            Id = FlightWorlds.TestRangeMap, Name = "House", Scene = TestRangeScene,
            Line = "The test range: a yard with houses, a car, fences, trees and wires",
            World = FlightWorlds.TestRange, Starts = root => FlightWorlds.TestRange(root).StartPoints,
        };

        public static readonly PlayMap Village = new PlayMap
        {
            Id = FlightWorlds.FarmlandMap, Name = "Village", Scene = FarmlandExplore.Scene,
            Line = "The farmland: fields, tree belts and a ruined village on rolling hills",
            World = FlightWorlds.Farmland, Starts = root => FlightWorlds.FarmlandStarts,
        };

        public static readonly PlayMap[] All = { House, Village };

        /// <summary>The map whose flights the scene loads; null for any other scene.</summary>
        public static PlayMap ForScene(string scene) => Array.Find(All, m => m.Scene == scene);

        /// <summary>
        /// The map's still, rendered through the feed by task 1.2 into a git-ignored folder: <c>unity/Generated/stills/&lt;map
        /// id&gt;.png</c> in the editor, and the build's copy, <c>generated/stills/&lt;map id&gt;.png</c> under the player's
        /// StreamingAssets.
        /// </summary>
        public static string StillPath(PlayMap map) => Application.isEditor
            ? Path.Combine(AppData.Root, "unity", "Generated", "stills", map.Id + ".png")
            : Path.Combine(AppData.Root, "generated", "stills", map.Id + ".png");

        /// <summary>The image at the path, or null when there is none (the card is drawn plain).</summary>
        public static Texture2D LoadStill(string path)
        {
            if (!File.Exists(path)) return null;
            var still = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = Path.GetFileName(path), hideFlags = HideFlags.DontSave };
            if (still.LoadImage(File.ReadAllBytes(path), true)) return still;
            Debug.LogWarning("[App] the still " + path + " could not be read; its card is drawn plain");
            UnityEngine.Object.Destroy(still);
            return null;
        }
    }
}
