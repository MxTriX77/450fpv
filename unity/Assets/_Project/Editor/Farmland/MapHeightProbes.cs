using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Fpv450.Assets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fpv450
{
    /// <summary>
    /// Fills a compiled map package's height probes with Unity's own answer (w20-hunter-track 1.4): the package's one map
    /// tile becomes a temporary terrain, and each probe's y in maps/&lt;id&gt;/height_probes.json is Terrain.SampleHeight at its
    /// x and z. Nothing is saved but that file. tools/map/compile_track.py --probe runs it and then recompiles:
    /// Unity -batchmode -nographics -quit -projectPath unity -executeMethod Fpv450.MapHeightProbes.Run -probeMap hunter-09
    /// </summary>
    public static class MapHeightProbes
    {
        const float MaxSample = 32766f;
        static readonly Regex Row = new Regex("\"x\": ([0-9.eE+-]+), \"z\": ([0-9.eE+-]+), \"y\": (null|[0-9.eE+-]+)", RegexOptions.Compiled);

        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-probeMap");
            if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("-probeMap <map id> is required");
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "..", "maps", args[at + 1]);

            Dictionary<string, object> manifest = Json.Obj(Json.Parse(File.ReadAllText(Path.Combine(folder, "map.json"))), "map.json");
            List<object> tiles = Json.Arr(manifest, "tiles", "map.json");
            if (tiles.Count != 1) throw new InvalidOperationException("this tool samples a package of one map tile");
            Dictionary<string, object> tile = Json.Obj(tiles[0], "map.json tile");
            Vector3 position = Json.Vec3(Json.Get(tile, "position_m", "tile"), "position_m");
            Vector3 size = Json.Vec3(Json.Get(tile, "size_m", "tile"), "size_m");
            Dictionary<string, object> heights = Json.Obj(tile, "heights", "tile");
            int res = (int)Json.Num(heights, "resolution", "heights");
            byte[] data = File.ReadAllBytes(Path.Combine(folder, Json.Str(heights, "file", "heights")));
            if (data.Length != 2 * res * res) throw new InvalidOperationException("the heights are not resolution x resolution samples");

            var grid = new float[res, res]; // Unity's order: [z from the south, x]; the file's rows run from the north
            for (int r = 0; r < res; r++)
                for (int c = 0; c < res; c++)
                    grid[res - 1 - r, c] = (data[2 * (r * res + c)] | data[2 * (r * res + c) + 1] << 8) / MaxSample;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var terrainData = new TerrainData { heightmapResolution = res, size = size };
            terrainData.SetHeights(0, 0, grid);
            GameObject go = Terrain.CreateTerrainGameObject(terrainData);
            go.transform.position = position;
            var terrain = go.GetComponent<Terrain>();

            string path = Path.Combine(folder, "height_probes.json");
            string[] lines = File.ReadAllLines(path);
            int filled = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                Match m = Row.Match(lines[i]);
                if (!m.Success) continue;
                double x = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                double z = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                float y = terrain.SampleHeight(new Vector3((float)x, 0f, (float)z)) + position.y;
                string text = Math.Round(y, 6).ToString("0.0#####", CultureInfo.InvariantCulture);
                lines[i] = lines[i].Substring(0, m.Groups[3].Index) + text + lines[i].Substring(m.Groups[3].Index + m.Groups[3].Length);
                filled++;
            }
            File.WriteAllText(path, string.Join("\n", lines) + "\n");
            Debug.Log($"MapHeightProbes: {filled} probes of {args[at + 1]} from Terrain.SampleHeight");
        }
    }
}
