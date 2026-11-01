using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Fpv450.Input.Radio;
using Fpv450.Sim.Flight;

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
        /// The test range (D-044, D-047): its start points from its placement file. Its queries (p6-world-query-gallery
        /// 1.4, MapWorld.Load(root, "test-range")) have not landed, so it flies on the world-query stub's floor at
        /// y = 0, which is the range's flat ground, and the log names the world "floor" so replay reopens that floor.
        /// MapWorld drops in here, with the id "test-range", and nothing else changes.
        /// </summary>
        public static FlightWorldChoice TestRange(string root) => new FlightWorldChoice
        {
            Id = "floor",
            Open = FlightWorld.Floor,
            StartPoints = ReadStartPoints(File.ReadAllText(PlacementsPath(root), Encoding.UTF8)),
        };

        /// <summary>
        /// A placement file's start points (tools/map/make_test_range.py: id, position_m in Unity's frame, which is the
        /// sim's, heading_deg, stand), in the file's order. The height is the world's ground, sampled at the flight's build.
        /// </summary>
        public static StartPoint[] ReadStartPoints(string placementsJson)
        {
            Dictionary<string, object> file = JsonRead.Object(MiniJson.Parse(placementsJson), "the placement file");
            if (!(JsonRead.Member(file, "start_points") is List<object> rows) || rows.Count == 0)
                throw new FormatException("the placement file lists no start points");
            var points = new StartPoint[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                Dictionary<string, object> row = JsonRead.Object(rows[i], "start point " + i);
                string id = JsonRead.Text(row, "id");
                if (!(JsonRead.Member(row, "position_m") is List<object> p) || p.Count != 3 || !(p[0] is double x) || !(p[2] is double z))
                    throw new FormatException("start point '" + id + "': position_m is not [x, y, z]");
                if (!(JsonRead.Member(row, "heading_deg") is double heading))
                    throw new FormatException("start point '" + id + "': heading_deg is not a number");
                points[i] = new StartPoint { Name = id, X = x, Z = z, Yaw = heading * Math.PI / 180, Stand = JsonRead.Bool(row, "stand") };
            }
            return points;
        }
    }
}
