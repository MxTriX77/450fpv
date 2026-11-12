using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;
using MapStart = Fpv450.Sim.World.StartPoint;

namespace Fpv450.App.Session
{
    /// <summary>
    /// The world a flight flies over, behind one seam (design 6): its id for the log header (replay reopens the world
    /// by it), a new FlightWorld for each flight, and its start points. SessionInputs takes the three as they are.
    /// </summary>
    public sealed class FlightWorldChoice
    {
        public string Id;
        public Func<FlightWorld> Open;
        public StartPoint[] StartPoints;

        public void ApplyTo(SessionInputs inputs)
        {
            inputs.WorldId = Id;
            inputs.World = Open;
            inputs.StartPoints = StartPoints;
        }
    }

    /// <summary>
    /// The worlds a flight flies: the farmland, the player's (D-088, w3-farmland-ground 3.1: from its village's start only,
    /// D-092), and the test range (D-047, D-077: from its houses' start), which left the player and stays for the headless
    /// tests and the benches.
    /// </summary>
    public static class FlightWorlds
    {
        public const string TestRangeMap = "test-range", FarmlandMap = "farmland";

        /// <summary>
        /// The one start the app flies from (D-077, the pilot: "we only load the one with many houses"): the map's
        /// `track`, by the yard and its houses. The map keeps its other start, the field's launch stand, for the benches;
        /// the setup's legs still decide whether the drone stands on its legs or on the stand.
        /// </summary>
        public const string HousesStart = "track";

        /// <summary>
        /// The farmland's one start point (D-092, w1-farmland-world design 6): in the village, on the hilltop meadow at the
        /// track's verge by the crossroads, the ground level and nothing within 21 m; heading 300°, at the nearest ruin. In
        /// the scene's frame, its foot on the terrain. The flight starts there, and the explorer stands the launch stand
        /// there (FarmlandExplore).
        /// </summary>
        public static readonly StartPoint FarmlandStart = new StartPoint { Name = "village", X = 566.5, Z = 569.0, Yaw = 300 * Math.PI / 180 };

        /// <summary>The farmland's starts for the setup, without loading its map (its package has none: FarmlandStart).</summary>
        public static StartPoint[] FarmlandStarts => new[] { FarmlandStart };

        /// <summary>
        /// The farmland on its queries, MapWorld.Load(root, "farmland") (w3-farmland-ground 1.1: its terrain's 17 tiles, no
        /// objects until 1.2), loaded once (about 0.3 s, 28 MB) and shared by its flights, as the test range's; its one start
        /// is FarmlandStart. The log names it by its map id, so replay and the summary reopen it.
        /// </summary>
        public static FlightWorldChoice Farmland(string root)
        {
            MapWorld map = MapWorld.Load(root, FarmlandMap);
            return new FlightWorldChoice { Id = map.MapId, Open = () => new FlightWorld(map), StartPoints = FarmlandStarts };
        }

        /// <summary>
        /// The files under the root that MapWorld reads for the farmland, by their paths from the root: the package's map.json,
        /// each tile's heights, classes and hole mask as it lists them (Terrain.Load checks each against the file's SHA-256),
        /// and the material table (a player's copies).
        /// </summary>
        public static List<string> FarmlandFiles(string root)
        {
            string folder = "maps/" + FarmlandMap + "/";
            var files = new List<string> { folder + "map.json" };
            string manifest = File.ReadAllText(Path.Combine(root, folder + "map.json"));
            foreach (Match m in Regex.Matches(manifest, "\"(?:heights|classes|holes)\"\\s*:\\s*\\{\\s*\"file\"\\s*:\\s*\"([^\"]+)\""))
                if (!files.Contains(folder + m.Groups[1].Value)) files.Add(folder + m.Groups[1].Value);
            files.Add(MapWorld.MaterialsFile);
            return files;
        }

        /// <summary>The test range's placement file, from the root that mirrors the repository (the editor's repository, the
        /// player's StreamingAssets).</summary>
        public static string PlacementsPath(string root) => Path.Combine(root, "maps", TestRangeMap, "placements.json");

        /// <summary>
        /// The test range (D-044, D-047) on its queries, MapWorld.Load(root, "test-range"), loaded once and shared by its
        /// flights: each flight's FlightWorld resets the map's runtime objects and adds its own (FlightCore). The log names
        /// the world by its map id, so replay and the summary reopen it; the start point is the map's HousesStart (D-077).
        /// </summary>
        public static FlightWorldChoice TestRange(string root)
        {
            MapWorld map = MapWorld.Load(root, TestRangeMap);
            MapStart[] starts = Array.FindAll(map.StartPoints, p => p.Id == HousesStart);
            if (starts.Length == 0) throw new FormatException("the test range has no '" + HousesStart + "' start point");
            return new FlightWorldChoice
            {
                Id = map.MapId,
                Open = () => new FlightWorld(map),
                StartPoints = Array.ConvertAll(starts, AppStart),
            };
        }

        /// <summary>
        /// The files under the root that MapWorld reads for the test range, by their paths from the root: the placement
        /// file, the material table and each placed variant's metadata, in the order it reads them (a player's copies).
        /// </summary>
        public static List<string> TestRangeFiles(string root)
        {
            var files = new List<string> { "maps/" + TestRangeMap + "/placements.json", MapWorld.MaterialsFile };
            string Read(string relative) => File.ReadAllText(Path.Combine(root, relative));
            new MapWorld(Read(files[0]), model =>
            {
                files.Add(MapWorld.ModelsRoot + "/" + model);
                return Read(MapWorld.ModelsRoot + "/" + model);
            }, MapWorld.MaterialNames(Read(files[1])));
            return files;
        }

        // The map's start point for the screens and the session: its foot on the ground and its heading in radians.
        static StartPoint AppStart(MapStart p) => new StartPoint
        {
            Name = p.Id, X = p.Position.X, Z = p.Position.Z, Yaw = p.HeadingDeg * Math.PI / 180, Stand = p.Stand,
        };
    }
}
