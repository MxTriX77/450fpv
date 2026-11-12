using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fpv450
{
    /// <summary>
    /// Writes the farmland's map package, <c>maps/farmland/</c> (w2-farmland-export 1.1; D-088, D-091), from the
    /// scene the friend's seeded generator (<c>FarmLandBuilder</c>) built. It reads the scene and its terrain data
    /// and changes nothing: the heights of all 17 terrain tiles (the map, the 8 surround tiles, the 8 far-ring
    /// tiles) as Unity stores them, each tile's ground class per alphamap sample, the map's grass counts per detail
    /// cell, every tree, bush, ruin and debris placement on the map, the field and village outlines, and 1,000
    /// seeded heights from <c>Terrain.SampleHeight</c> that the validator checks the package against. The format
    /// is <c>maps/farmland/README.md</c>; <c>tools/map/validate.py</c> checks it.
    /// The generator's own layout (fields, village, seeds) is read by reflection, so this file compiles without it
    /// and never edits it. Engine-free code never sees this file: it only reads the package.
    ///
    ///   Unity -batchmode -nographics -quit -projectPath &lt;project&gt; -executeMethod Fpv450.FarmlandExport.Run
    ///         [-exportOut &lt;dir&gt;] [-exportScene &lt;Assets/...unity&gt;] -logFile -
    ///
    /// -exportOut defaults to the repository's maps/farmland (the project's ../maps/farmland); -exportScene to the
    /// one scene asset named FarmLand. Exit code 1 on any error. The same scene gives the same bytes.
    /// </summary>
    public static class FarmlandExport
    {
        const string Tag = "[FarmlandExport]";
        const int FormatVersion = 1;

        // Unity keeps a heightmap sample as an integer 0..32766 and hands it out as sample / 32766 (checked below).
        const double MaxSample = 32766.0;

        // The probes: 500 on the map, 300 on the surround, 200 on the far ring, at multiples of 1/1024 m (exact in a
        // float and in decimal), from SplitMix64 seeded with ProbeSeed.
        const ulong ProbeSeed = 450;
        const int ProbesMap = 500, ProbesSurround = 300, ProbesFar = 200;
        const double ProbeGrid = 1024.0;

        static readonly string[] Directions = { "SW", "S", "SE", "W", "E", "NW", "N", "NE" };

        // The 8 ground layers' surface classes (assets-src/surfaces.json; id = its row in that file, MapWorld.SurfaceIds + 1).
        static readonly (string layer, string surface, int id, string note)[] LayerClasses =
        {
            ("GrassGround", "meadow_sod", 1, "assumed: the meadow's ground under green grass, root-bound sod"),
            ("GrassA", "meadow_sod", 1, "assumed: the meadow's green grass, root-bound sod"),
            ("GrassDry", "weeds", 8, "assumed: dry grass, on the fields' weed patches and the meadow's dried-out areas; firmer loam than the green sod"),
            ("GrassPath", "dirt_track", 13, "assumed: the 4 m grassy side tracks with two wheel ruts, wheel-compacted"),
            ("Gravel", "dirt_track", 13, "assumed: the 6 m gravel tracks, 35-80 % dry mud; no gravel class, the nearest is the wheel-compacted track (it also rings the ruins as rubble)"),
            ("AerialMud_0", "tilled", 6, "assumed: an abandoned field's furrows, rows along world X"),
            ("AerialMud_90", "tilled", 6, "assumed: an abandoned field's furrows, rows along world Z"),
            ("DryMud", "dry_crust", 2, "assumed: dry, crusted bare mud on the fields, the house yards and the sun-burnt meadow; with gravel, the rule below"),
        };

        // The physics sign-off's one rule (w2-farmland-export design.md §5): where gravel is painted, the dry mud is the
        // track's (or a yard's) mud film on a compacted body, so on any sample with Gravel weight > 0, DryMud's weight
        // counts toward dirt_track instead of dry_crust.
        static readonly (string layer, string whenLayer, string surface, int id, string note) GravelRule =
            ("DryMud", "Gravel", "dirt_track", 13, "physics sign-off: on any sample with Gravel weight > 0, DryMud is the track's or a yard's mud film on a compacted body, so its weight counts toward dirt_track, not dry_crust");

        sealed class Tile
        {
            public string Id, Ring;
            public Terrain Terrain;
            public TerrainData Data => Terrain.terrainData;
            public Vector3 Position => Terrain.transform.position;
            public bool Contains(double x, double z) =>
                x >= Position.x && x <= Position.x + Data.size.x && z >= Position.z && z <= Position.z + Data.size.z;
        }

        public static void Run()
        {
            try
            {
                string project = Path.GetDirectoryName(Application.dataPath)!;
                string output = Argument("-exportOut") ?? Path.GetFullPath(Path.Combine(project, "..", "maps", "farmland"));
                if (!Directory.Exists(Path.GetDirectoryName(output)))
                    throw new DirectoryNotFoundException($"{Path.GetDirectoryName(output)} does not exist: pass -exportOut <dir>");
                Export(Argument("-exportScene") ?? FindScene(), output);
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
        }

        static void Export(string scenePath, string output)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Type builder = FindBuilder();
            List<Tile> tiles = FindTiles();
            Tile map = tiles[0];

            foreach (string folder in new[] { "heights", "classes", "grass" })
            {
                string path = Path.Combine(output, folder);
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                Directory.CreateDirectory(path);
            }

            var files = new SortedDictionary<string, string>(StringComparer.Ordinal); // relative path -> SHA-256
            var tileJson = new List<string>();
            foreach (Tile tile in tiles)
            {
                (int heightRes, int minSample, int maxSample) = WriteHeights(tile, output, files);
                int classRes = WriteClasses(tile, output, files);
                tileJson.Add(
                    $"    {{\"id\": {Str(tile.Id)}, \"ring\": {Str(tile.Ring)}, \"position_m\": {Vec(tile.Position, 6)}, \"size_m\": {Vec(tile.Data.size, 6)},\n" +
                    $"     \"heights\": {{\"file\": {Str($"heights/{tile.Id}.r16")}, \"resolution\": {heightRes}, \"min_sample\": {minSample}, \"max_sample\": {maxSample}}},\n" +
                    $"     \"classes\": {{\"file\": {Str($"classes/{tile.Id}.raw")}, \"resolution\": {classRes}}}}}");
            }

            string grassJson = WriteGrass(map, builder, output, files);
            (string placements, Dictionary<string, int> counts) = Placements(map, tiles);
            Write(output, "placements.json", placements, files);
            Write(output, "outlines.json", Outlines(builder), files);
            (string probes, double colliderGap, int colliderProbes) = Probes(tiles);
            Write(output, "height_probes.json", probes, files);

            var json = new StringBuilder();
            json.Append("{\n");
            json.Append("  \"format\": \"fpv450-map-package\",\n");
            json.Append($"  \"format_version\": {FormatVersion},\n");
            json.Append("  \"map\": \"farmland\",\n");
            json.Append("  \"frame\": \"unity-lh-yup\",\n");
            json.Append($"  \"source\": {{\"scene\": {Str(Path.GetFileNameWithoutExtension(scenePath))}, \"generator\": \"FarmLandBuilder\", \"exporter\": \"Fpv450.FarmlandExport\", \"seeds\": {Seeds(builder)}}},\n");
            json.Append($"  \"map_rect_m\": [{Num(map.Position.x, 6)}, {Num(map.Position.z, 6)}, {Num(map.Position.x + map.Data.size.x, 6)}, {Num(map.Position.z + map.Data.size.z, 6)}],\n");
            json.Append("  \"tiles\": [\n").Append(string.Join(",\n", tileJson)).Append("\n  ],\n");
            json.Append("  \"ground_classes\": {\"surface_table\": \"assets-src/surfaces.json\", \"layers\": [\n");
            json.Append(string.Join(",\n", LayerClasses.Select(c => $"    {{\"layer\": {Str(c.layer)}, \"surface\": {Str(c.surface)}, \"id\": {c.id}, \"note\": {Str(c.note)}}}")));
            json.Append("\n  ], \"rules\": [\n");
            json.Append($"    {{\"layer\": {Str(GravelRule.layer)}, \"when\": {{\"layer\": {Str(GravelRule.whenLayer)}, \"weight_above\": 0}}, \"surface\": {Str(GravelRule.surface)}, \"id\": {GravelRule.id}, \"note\": {Str(GravelRule.note)}}}");
            json.Append("\n  ]},\n");
            json.Append(grassJson).Append(",\n");
            json.Append($"  \"counts\": {{{string.Join(", ", counts.Select(c => $"{Str(c.Key)}: {c.Value}"))}}},\n");
            json.Append($"  \"height_probes\": {{\"seed\": {ProbeSeed}, \"map\": {ProbesMap}, \"surround\": {ProbesSurround}, \"far\": {ProbesFar}}},\n");
            json.Append("  \"files\": {\n");
            json.Append(string.Join(",\n", files.Select(f => $"    {Str(f.Key)}: {Str(f.Value)}")));
            json.Append("\n  }\n}\n");
            Write(output, "map.json", json.ToString(), null);

            Debug.Log($"{Tag} wrote {output}: {tiles.Count} tiles, {string.Join(", ", counts.Select(c => $"{c.Value} {c.Key}"))}; " +
                      (colliderProbes > 0 ? $"terrain colliders vs SampleHeight at {colliderProbes} probes: max {colliderGap * 1000:F4} mm" : "no terrain collider answered a probe"));
        }

        // ------------------------------------------------------------------ the tiles

        static List<Tile> FindTiles()
        {
            Terrain[] all = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Tile Named(string name, string id, string ring)
            {
                Terrain[] found = all.Where(t => t.gameObject.name == name).ToArray();
                if (found.Length != 1 || found[0].terrainData == null)
                    throw new InvalidOperationException($"the scene must hold exactly one terrain named '{name}' with data; it holds {found.Length}");
                return new Tile { Id = id, Ring = ring, Terrain = found[0] };
            }

            var tiles = new List<Tile> { Named("Terrain", "map", "map") };
            tiles.AddRange(Directions.Select(d => Named($"Surround {d}", $"surround_{d}", "surround")));
            tiles.AddRange(Directions.Select(d => Named($"Far Ring {d}", $"far_{d}", "far")));
            if (all.Length != tiles.Count)
                throw new InvalidOperationException($"the scene holds {all.Length} terrains; the export knows {tiles.Count}");
            return tiles;
        }

        // Rows north to south (row 0 is the +Z edge, D-019), columns west to east; uint16 little-endian samples.
        static (int resolution, int min, int max) WriteHeights(Tile tile, string output, IDictionary<string, string> files)
        {
            TerrainData data = tile.Data;
            int res = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, res, res);
            var bytes = new byte[res * res * 2];
            int min = int.MaxValue, max = int.MinValue, at = 0;
            for (int row = 0; row < res; row++)
            {
                int z = res - 1 - row;
                for (int x = 0; x < res; x++)
                {
                    double scaled = heights[z, x] * MaxSample;
                    int sample = (int)Math.Round(scaled);
                    if (Math.Abs(scaled - sample) > 0.01 || sample < 0 || sample > MaxSample)
                        throw new InvalidOperationException($"{tile.Id}: height {heights[z, x]} at ({x}, {z}) is not a sample / {MaxSample}");
                    min = Math.Min(min, sample);
                    max = Math.Max(max, sample);
                    bytes[at++] = (byte)sample;
                    bytes[at++] = (byte)(sample >> 8);
                }
            }
            Write(output, $"heights/{tile.Id}.r16", bytes, files);
            return (res, min, max);
        }

        // The class of each alphamap sample: the surface whose layers weigh most there (ties to the lower id), after
        // GravelRule moves DryMud's weight to dirt_track wherever Gravel weighs anything.
        static int WriteClasses(Tile tile, string output, IDictionary<string, string> files)
        {
            TerrainData data = tile.Data;
            int res = data.alphamapResolution;
            if (data.alphamapWidth != res || data.alphamapHeight != res)
                throw new InvalidOperationException($"{tile.Id}: alphamap {data.alphamapWidth} x {data.alphamapHeight}, not square");
            int ruleLayer = Array.FindIndex(data.terrainLayers, layer => layer.name == GravelRule.layer);
            int whenLayer = Array.FindIndex(data.terrainLayers, layer => layer.name == GravelRule.whenLayer);
            int[] layerClass = data.terrainLayers.Select(layer =>
            {
                int found = Array.FindIndex(LayerClasses, c => c.layer == layer.name);
                if (found < 0)
                    throw new InvalidOperationException($"{tile.Id}: terrain layer '{layer.name}' has no surface class");
                return LayerClasses[found].id;
            }).ToArray();
            float[,,] alpha = data.GetAlphamaps(0, 0, res, res);
            var bytes = new byte[res * res];
            var weight = new double[256];
            int at = 0;
            for (int row = 0; row < res; row++)
            {
                int z = res - 1 - row;
                for (int x = 0; x < res; x++)
                {
                    Array.Clear(weight, 0, weight.Length);
                    bool gravel = whenLayer >= 0 && alpha[z, x, whenLayer] > 0;
                    for (int layer = 0; layer < layerClass.Length; layer++)
                        weight[gravel && layer == ruleLayer ? GravelRule.id : layerClass[layer]] += alpha[z, x, layer];
                    int best = 0;
                    for (int id = 1; id < weight.Length; id++)
                    {
                        if (weight[id] > weight[best])
                            best = id;
                    }
                    if (best == 0)
                        throw new InvalidOperationException($"{tile.Id}: no layer weighs anything at alphamap sample ({x}, {z})");
                    bytes[at++] = (byte)best;
                }
            }
            Write(output, $"classes/{tile.Id}.raw", bytes, files);
            return res;
        }

        // The map's detail layers: one plane per layer, rows north to south, the instance count per cell as a byte.
        static string WriteGrass(Tile map, Type builder, string output, IDictionary<string, string> files)
        {
            TerrainData data = map.Data;
            int width = data.detailWidth, height = data.detailHeight;
            if (width != height)
                throw new InvalidOperationException($"map: detail map {width} x {height}, not square");
            if (data.detailScatterMode != DetailScatterMode.InstanceCountMode)
                throw new InvalidOperationException($"map: detail scatter mode {data.detailScatterMode}, not instance counts");

            DetailPrototype[] prototypes = data.detailPrototypes;
            Array types = (Array)Field(builder, "GrassTypes").GetValue(null);
            if (types.Length != prototypes.Length)
                throw new InvalidOperationException($"the generator has {types.Length} grass types, the map {prototypes.Length} detail layers");

            var bytes = new byte[prototypes.Length * width * width];
            var layers = new List<string>();
            int at = 0;
            for (int layer = 0; layer < prototypes.Length; layer++)
            {
                DetailPrototype prototype = prototypes[layer];
                object type = types.GetValue(layer);
                string prefab = (string)type.GetType().GetField("Item1").GetValue(type);
                string group = type.GetType().GetField("Item3").GetValue(type).ToString();
                if (prototype.prototype == null || prototype.prototype.name != prefab)
                    throw new InvalidOperationException($"detail layer {layer} is {(prototype.prototype ? prototype.prototype.name : "empty")}, the generator's type {prefab}");
                Bounds mesh = prototype.prototype.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;

                int[,] counts = data.GetDetailLayer(0, 0, width, width, layer);
                int total = 0;
                for (int row = 0; row < width; row++)
                {
                    int z = width - 1 - row;
                    for (int x = 0; x < width; x++)
                    {
                        int count = counts[z, x];
                        if (count < 0 || count > 255)
                            throw new InvalidOperationException($"map: detail layer {layer} holds {count} at ({x}, {z})");
                        bytes[at++] = (byte)count;
                        total += count;
                    }
                }
                layers.Add($"    {{\"layer\": {layer}, \"prefab\": {Str(prefab)}, \"group\": {Str(group)}, \"width_scale\": [{Num(prototype.minWidth, 4)}, {Num(prototype.maxWidth, 4)}], " +
                           $"\"height_scale\": [{Num(prototype.minHeight, 4)}, {Num(prototype.maxHeight, 4)}], \"mesh_y_m\": [{Num(mesh.min.y, 4)}, {Num(mesh.max.y, 4)}], \"instances\": {total}}}");
            }
            Write(output, "grass/map.raw", bytes, files);
            return $"  \"grass\": {{\"tile\": \"map\", \"file\": \"grass/map.raw\", \"resolution\": {width}, \"layers\": [\n{string.Join(",\n", layers)}\n  ]}}";
        }

        // ------------------------------------------------------------------ the placements

        static (string json, Dictionary<string, int> counts) Placements(Tile map, List<Tile> tiles)
        {
            var rows = new List<string>();
            var variants = new SortedDictionary<string, string>(StringComparer.Ordinal); // id -> kind
            var counts = new Dictionary<string, int> { ["tree"] = 0, ["bush"] = 0, ["ruin"] = 0, ["debris"] = 0 };
            Vector3 origin = map.Position, size = map.Data.size;

            void Add(string kind, string variant, Vector3 position, Quaternion rotation, Vector3 scale)
            {
                if (variants.TryGetValue(variant, out string known) && known != kind)
                    throw new InvalidOperationException($"variant {variant} is both a {known} and a {kind}");
                variants[variant] = kind;
                if (position.x < origin.x || position.x > origin.x + size.x || position.z < origin.z || position.z > origin.z + size.z)
                    throw new InvalidOperationException($"{kind} {variant} at {position} lies outside the map");
                counts[kind]++;
                rows.Add($"    {{\"id\": {rows.Count}, \"kind\": {Str(kind)}, \"variant\": {Str(variant)}, \"position_m\": {Vec(position, 4)}, " +
                         $"\"rotation_xyzw\": [{Num(rotation.x, 7)}, {Num(rotation.y, 7)}, {Num(rotation.z, 7)}, {Num(rotation.w, 7)}], \"scale\": {Vec(scale, 5)}}}");
            }

            // Trees and bushes: the map's terrain instances, in Unity's order. A prototype's folder names its kind.
            TreePrototype[] prototypes = map.Data.treePrototypes;
            foreach (TreeInstance instance in map.Data.treeInstances)
            {
                GameObject prefab = prototypes[instance.prototypeIndex].prefab;
                string folder = Path.GetFileName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(prefab)));
                string kind = folder == "Trees" ? "tree" : folder == "Bushes" ? "bush" : throw new InvalidOperationException($"tree prototype {prefab.name} is in '{folder}', neither Trees nor Bushes");
                Vector3 position = origin + Vector3.Scale(instance.position, size);
                double half = instance.rotation * 0.5; // radians about +Y, as Quaternion.Euler(0, rotation in degrees, 0)
                Add(kind, prefab.name, position, new Quaternion(0f, (float)Math.Sin(half), 0f, (float)Math.Cos(half)),
                    new Vector3(instance.widthScale, instance.heightScale, instance.widthScale));
            }

            // Ruins and debris: the Village object's prefab instances, in hierarchy order.
            GameObject village = GameObject.Find("Village") ?? throw new InvalidOperationException("the scene has no Village object");
            foreach (Transform child in village.transform)
            {
                GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject)
                    ?? throw new InvalidOperationException($"Village/{child.name} is not a prefab instance");
                Add(prefab.name.StartsWith("Ruin_", StringComparison.Ordinal) ? "ruin" : "debris", prefab.name, child.position, child.rotation, child.lossyScale);
            }

            // Nothing else on the map draws: every renderer inside the map's rectangle belongs to the village.
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Vector3 c = renderer.bounds.center;
                bool onMap = c.x >= origin.x && c.x <= origin.x + size.x && c.z >= origin.z && c.z <= origin.z + size.z;
                if (onMap && !renderer.transform.IsChildOf(village.transform))
                    throw new InvalidOperationException($"{renderer.gameObject.name} draws on the map and has no placement");
            }

            int instances = map.Data.treeInstanceCount, villageObjects = village.transform.childCount;
            if (counts["tree"] + counts["bush"] != instances || counts["ruin"] + counts["debris"] != villageObjects)
                throw new InvalidOperationException("the placements don't account for every terrain instance and village object");
            counts["ring_trees_left_out"] = tiles.Where(t => t.Ring != "map").Sum(t => t.Data.treeInstanceCount);

            var json = new StringBuilder();
            json.Append("{\n  \"format\": \"fpv450-map-placements\",\n");
            json.Append($"  \"format_version\": {FormatVersion},\n  \"map\": \"farmland\",\n  \"frame\": \"unity-lh-yup\",\n");
            json.Append("  \"variants\": [\n").Append(string.Join(",\n", variants.Select(v => $"    {{\"id\": {Str(v.Key)}, \"kind\": {Str(v.Value)}}}"))).Append("\n  ],\n");
            json.Append("  \"placements\": [\n").Append(string.Join(",\n", rows)).Append("\n  ]\n}\n");
            return (json.ToString(), counts);
        }

        // ------------------------------------------------------------------ the outlines

        static string Outlines(Type builder)
        {
            var fields = new List<string>();
            foreach (object field in (Array)Field(builder, "Fields").GetValue(null))
            {
                Type type = field.GetType();
                var outline = (Vector2[])type.GetField("Outline").GetValue(field);
                fields.Add($"    {{\"name\": {Str((string)type.GetField("Name").GetValue(field))}, " +
                           $"\"overgrowth\": {Num((float)type.GetField("Overgrowth").GetValue(field), 4)}, " +
                           $"\"furrow_axis\": {Str((string)type.GetProperty("FurrowAxis").GetValue(field))},\n" +
                           $"     \"outline_m\": [{string.Join(", ", outline.Select(p => $"[{Num(p.x, 4)}, {Num(p.y, 4)}]"))}]}}");
            }
            var centre = (Vector2)Field(builder, "VillageCentre").GetValue(null);
            float radius = Convert.ToSingle(Field(builder, "VillageRadius").GetValue(null), CultureInfo.InvariantCulture);
            return "{\n  \"format\": \"fpv450-map-outlines\",\n" +
                   $"  \"format_version\": {FormatVersion},\n  \"map\": \"farmland\",\n  \"frame\": \"unity-lh-yup\",\n" +
                   "  \"fields\": [\n" + string.Join(",\n", fields) + "\n  ],\n" +
                   $"  \"village\": {{\"shape\": \"circle\", \"centre_m\": [{Num(centre.x, 4)}, {Num(centre.y, 4)}], \"radius_m\": {Num(radius, 4)}}}\n}}\n";
        }

        static string Seeds(Type builder)
        {
            IEnumerable<FieldInfo> seeds = builder.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(int) && f.Name.EndsWith("Seed", StringComparison.Ordinal))
                .OrderBy(f => f.Name, StringComparer.Ordinal);
            return "{" + string.Join(", ", seeds.Select(f => $"{Str(f.Name)}: {(int)f.GetValue(null)}")) + "}";
        }

        // ------------------------------------------------------------------ the height probes

        // Each probe's height is the highest Terrain.SampleHeight among the tiles that hold its point (the far ring's
        // side tiles reach under the surround block). The terrain colliders are asked too, for the log only.
        static (string json, double colliderGap, int colliderProbes) Probes(List<Tile> tiles)
        {
            Rect Block(string ring)
            {
                Tile[] ofRing = tiles.Where(t => t.Ring == ring).ToArray();
                float xMin = ofRing.Min(t => t.Position.x), zMin = ofRing.Min(t => t.Position.z);
                float xMax = ofRing.Max(t => t.Position.x + t.Data.size.x), zMax = ofRing.Max(t => t.Position.z + t.Data.size.z);
                return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
            }
            Rect map = Block("map"), surround = Block("surround"), far = Block("far");

            ulong state = ProbeSeed;
            double Uniform(double low, double high)
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                double unit = (z >> 11) * (1.0 / (1UL << 53));
                return Math.Floor((low + unit * (high - low)) * ProbeGrid) / ProbeGrid;
            }

            bool Inside(Rect r, double x, double z) => x >= r.xMin && x <= r.xMax && z >= r.yMin && z <= r.yMax;

            Physics.SyncTransforms();
            var rows = new List<string>();
            double gap = 0;
            int answered = 0;
            foreach ((string ring, int count, Rect within, Rect outside) in new[]
                     { ("map", ProbesMap, map, Rect.zero), ("surround", ProbesSurround, surround, map), ("far", ProbesFar, far, surround) })
            {
                for (int i = 0; i < count;)
                {
                    double x = Uniform(within.xMin, within.xMax), z = Uniform(within.yMin, within.yMax);
                    if (ring != "map" && Inside(outside, x, z))
                        continue;
                    float y = float.NegativeInfinity;
                    var point = new Vector3((float)x, 0f, (float)z);
                    foreach (Tile tile in tiles.Where(t => t.Contains(x, z)))
                        y = Math.Max(y, tile.Terrain.SampleHeight(point) + tile.Position.y);
                    if (float.IsNegativeInfinity(y))
                        throw new InvalidOperationException($"probe ({x}, {z}) lies on no tile");

                    float hit = float.NegativeInfinity;
                    foreach (RaycastHit h in Physics.RaycastAll(new Vector3((float)x, 10000f, (float)z), Vector3.down, 20000f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider is TerrainCollider)
                            hit = Math.Max(hit, h.point.y);
                    }
                    if (!float.IsNegativeInfinity(hit))
                    {
                        gap = Math.Max(gap, Math.Abs(hit - y));
                        answered++;
                    }

                    rows.Add($"    {{\"ring\": {Str(ring)}, \"x\": {Exact(x)}, \"z\": {Exact(z)}, \"y\": {Num(y, 6)}}}");
                    i++;
                }
            }
            string json = "{\n  \"format\": \"fpv450-map-height-probes\",\n" +
                          $"  \"format_version\": {FormatVersion},\n  \"map\": \"farmland\",\n  \"frame\": \"unity-lh-yup\",\n" +
                          "  \"about\": \"Unity's Terrain.SampleHeight plus the tile's y, the highest among the tiles that hold the point\",\n" +
                          "  \"probes\": [\n" + string.Join(",\n", rows) + "\n  ]\n}\n";
            return (json, gap, answered);
        }

        // ------------------------------------------------------------------ helpers

        static Type FindBuilder()
        {
            Type builder = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("FarmLandBuilder", false)).FirstOrDefault(t => t != null);
            return builder ?? throw new InvalidOperationException("the friend's generator, FarmLandBuilder, is not in the project");
        }

        static FieldInfo Field(Type type, string name) =>
            type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{type.Name}.{name} is missing: the generator changed, update the export");

        static string FindScene()
        {
            string[] found = AssetDatabase.FindAssets("FarmLand t:Scene").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p) == "FarmLand").ToArray();
            if (found.Length != 1)
                throw new InvalidOperationException($"{found.Length} scenes named FarmLand: pass -exportScene <path>");
            return found[0];
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        static void Write(string output, string relative, string text, IDictionary<string, string> files) =>
            Write(output, relative, new UTF8Encoding(false).GetBytes(text), files);

        static void Write(string output, string relative, byte[] bytes, IDictionary<string, string> files)
        {
            File.WriteAllBytes(Path.Combine(output, relative), bytes);
            if (files == null)
                return;
            using SHA256 sha = SHA256.Create();
            files.Add(relative, string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2", CultureInfo.InvariantCulture))));
        }

        static string Num(double value, int decimals)
        {
            double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            if (rounded == 0)
                rounded = 0; // never "-0"
            return rounded.ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);
        }

        // A multiple of 1/1024 m, written exactly.
        static string Exact(double value) => Num(value, 10);

        static string Vec(Vector3 v, int decimals) => $"[{Num(v.x, decimals)}, {Num(v.y, decimals)}, {Num(v.z, decimals)}]";

        static string Str(string text)
        {
            var escaped = new StringBuilder("\"");
            foreach (char c in text)
            {
                if (c == '"' || c == '\\')
                    escaped.Append('\\').Append(c);
                else if (c < 0x20)
                    escaped.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else
                    escaped.Append(c);
            }
            return escaped.Append('"').ToString();
        }
    }
}
