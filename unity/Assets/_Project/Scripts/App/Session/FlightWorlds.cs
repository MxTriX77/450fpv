using System;
using System.Collections.Generic;
using System.IO;
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

    /// <summary>The worlds the app flies (D-047: the test range is the one map).</summary>
    public static class FlightWorlds
    {
        public const string TestRangeMap = "test-range";

        /// <summary>The test range's placement file, from the root that mirrors the repository (the editor's repository, the
        /// player's StreamingAssets).</summary>
        public static string PlacementsPath(string root) => Path.Combine(root, "maps", TestRangeMap, "placements.json");

        /// <summary>
        /// The test range (D-044, D-047) on its queries, MapWorld.Load(root, "test-range"), loaded once and shared by its
        /// flights: each flight's FlightWorld resets the map's runtime objects and adds its own (FlightCore). The log names
        /// the world by its map id, so replay and the summary reopen it; the start points are the map's, in its order.
        /// </summary>
        public static FlightWorldChoice TestRange(string root)
        {
            MapWorld map = MapWorld.Load(root, TestRangeMap);
            return new FlightWorldChoice
            {
                Id = map.MapId,
                Open = () => new FlightWorld(map),
                StartPoints = Array.ConvertAll(map.StartPoints, AppStart),
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
