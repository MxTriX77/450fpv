using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Step 9: hides the map edge. Eight terrain tiles surround the map. Each continues the map's edge heights
/// and eases into low rolling hills, painted as meadow. Grass grows near the border, and two broken belts of
/// trees plus scattered clumps fade into the haze. The tiles connect to the map, so their seams stitch.
/// Past them, a far ring of eight coarse 3 km tiles carries the same hills on to the horizon, so the land
/// doesn't end where the haze is still thin.
/// </summary>
public static partial class FarmLandBuilder
{
    const string SurroundObjectName = "Map Surround";
    const int SurroundAlphamapResolution = 512;
    const int SurroundDetailResolution = 512;
    const float SurroundPixelError = 8f;
    const int SurroundTreeSeed = 9301;

    // Heights ease from the map edge into the outer hills over this distance, in metres.
    const float SurroundBlendWidth = 250f;
    const float SurroundBaseHeight = 20f;
    const float SurroundHillHeight = 16f;

    // Grass grows near the map, since the grass draw distance is 200 m, and thins out away from it. The heavy
    // clump meshes stop sooner, since they cost about 1,000 triangles each.
    const float SurroundGrassReach = 170f;
    const float SurroundGrassThinning = 60f;
    const float SurroundGrassFarShare = 0.35f;
    const float SurroundHeavyGrassReach = 35f;
    static readonly string[] HeavyGrassPrefabs = { "Grass_B", "Grass_C", "Grass_D" };

    // Trees past this distance from the camera are mostly lost in the haze, so they aren't drawn. At the
    // Natural day haze, less than 10% of a tree still shows at 2.5 km.
    const float SurroundTreeDistance = 2500f;

    // The far ring: coarse tiles, unconnected to the rest, reaching a little under the surround's outer
    // edge and sunk there, so no crack shows along the seam.
    const string FarRingObjectName = "Map Far Ring";
    const float FarRingTileSize = 3000f;
    const int FarRingHeightmapResolution = 257;
    const int FarRingAlphamapResolution = 256;
    const float FarRingOverlap = 24f;
    const float FarRingSeamSink = 1.5f;
    const float FarRingSinkWidth = 150f;
    const float FarRingPixelError = 20f;

    // Tree belts: distance outside the map edge, spacing along the belt, and the share left after gaps.
    static readonly (float offset, float spacing, float keep, float jitter)[] SurroundBelts =
    {
        (70f, 14f, 0.55f, 6f),
        (300f, 22f, 0.45f, 10f),
    };
    const int SurroundClumps = 20;
    const float SurroundClumpMin = 150f;
    const float SurroundClumpMax = 450f;
    const float SurroundClumpRadius = 18f;
    const int SurroundClumpTrees = 4;

    // Island trees only, since they fall back to an 8-triangle card with distance, which keeps the many far
    // trees cheap. Pines keep a full mesh at any range.
    static readonly string[] SurroundTreeMix = { IslandPrefabName };

    static readonly (string name, int dx, int dz)[] SurroundTiles =
    {
        ("SW", -1, -1), ("S", 0, -1), ("SE", 1, -1),
        ("W", -1, 0), ("E", 1, 0),
        ("NW", -1, 1), ("N", 0, 1), ("NE", 1, 1),
    };

    [MenuItem("Farm Land/Step 9 - Map Edge")]
    public static void BuildMapEdge()
    {
        RequireEditMode();
        Scene scene = OpenFarmLandScene();
        Terrain main = GameObject.Find(TerrainObjectName).GetComponent<Terrain>();

        GameObject root = GameObject.Find(SurroundObjectName) ?? new GameObject(SurroundObjectName);
        GetOrAdd<SurroundGrassCulling>(root);
        List<PlacedTree>[] trees = PlaceSurroundTrees();

        for (int i = 0; i < SurroundTiles.Length; i++)
        {
            var tile = SurroundTiles[i];
            TerrainData data = LoadOrCreateSurroundData(tile.name);
            var origin = new Vector2(tile.dx * MapSize, tile.dz * MapSize);

            data.terrainLayers = main.terrainData.terrainLayers;
            data.alphamapResolution = SurroundAlphamapResolution;
            data.SetHeights(0, 0, SurroundHeights(main, origin, data.heightmapResolution));
            data.SetAlphamaps(0, 0, SurroundAlphamaps(origin));
            PaintSurroundGrass(data, origin);
            ApplySurroundTrees(data, trees[i], origin);
            EditorUtility.SetDirty(data);

            Terrain terrain = SurroundTerrain(root.transform, tile.name, data);
            terrain.transform.position = new Vector3(origin.x, main.transform.position.y, origin.y);
            CopyTerrainSettings(main, terrain);
        }

        BuildFarRing(main);

        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            camera.farClipPlane = CameraFarClip;

        Terrain.SetConnectivityDirty();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Map surround: {SurroundTiles.Length} tiles, {trees.Sum(list => list.Count)} trees");
    }

    // The surround block runs from -1 km to 2 km on both axes. The far ring's tiles sit around it.
    static void BuildFarRing(Terrain main)
    {
        GameObject root = GameObject.Find(FarRingObjectName) ?? new GameObject(FarRingObjectName);
        float inner = -MapSize;
        float outer = 2f * MapSize;

        foreach (var tile in SurroundTiles)
        {
            // Side tiles reach FarRingOverlap metres into the surround block. Corner tiles only touch it at a
            // point, so they don't overlap anything.
            float overlapX = tile.dz == 0 ? FarRingOverlap : 0f;
            float overlapZ = tile.dx == 0 ? FarRingOverlap : 0f;
            float minX = tile.dx < 0 ? inner - FarRingTileSize : tile.dx > 0 ? outer - overlapX : inner;
            float minZ = tile.dz < 0 ? inner - FarRingTileSize : tile.dz > 0 ? outer - overlapZ : inner;
            float sizeX = tile.dx == 0 ? outer - inner : FarRingTileSize + overlapX;
            float sizeZ = tile.dz == 0 ? outer - inner : FarRingTileSize + overlapZ;

            string path = $"{TerrainFolder}/FarRing_{tile.name}.asset";
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null)
            {
                data = new TerrainData { heightmapResolution = FarRingHeightmapResolution, alphamapResolution = FarRingAlphamapResolution, baseMapResolution = 256 };
                AssetDatabase.CreateAsset(data, path);
            }
            data.heightmapResolution = FarRingHeightmapResolution;
            data.size = new Vector3(sizeX, MapMaxHeight, sizeZ);
            data.terrainLayers = main.terrainData.terrainLayers;
            data.alphamapResolution = FarRingAlphamapResolution;

            int size = data.heightmapResolution;
            var heights = new float[size, size];
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    float worldX = minX + x * sizeX / (size - 1);
                    float worldZ = minZ + z * sizeZ / (size - 1);
                    float intoBlock = SurroundBlockDistance(worldX, worldZ);
                    float sink = FarRingSeamSink * Mathf.SmoothStep(1f, 0f, intoBlock / FarRingSinkWidth);
                    heights[z, x] = Mathf.Clamp01((SurroundHillHeightAt(worldX, worldZ) - sink) / MapMaxHeight);
                }
            }
            data.SetHeights(0, 0, heights);

            int resolution = FarRingAlphamapResolution;
            var alpha = new float[resolution, resolution, GroundLayerCount];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                    PaintMeadow(alpha, z, x, minX + (x + 0.5f) * sizeX / resolution, minZ + (z + 0.5f) * sizeZ / resolution);
            }
            data.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(data);

            string objectName = $"Far Ring {tile.name}";
            Transform existing = root.transform.Find(objectName);
            GameObject terrainObject = existing != null ? existing.gameObject : Terrain.CreateTerrainGameObject(data);
            terrainObject.name = objectName;
            terrainObject.transform.SetParent(root.transform, true);
            terrainObject.transform.position = new Vector3(minX, main.transform.position.y, minZ);
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.terrainData = data;
            TerrainCollider collider = terrainObject.GetComponent<TerrainCollider>();
            if (collider != null)
                collider.terrainData = data;
            terrain.materialTemplate = main.materialTemplate;
            terrain.drawInstanced = true;
            terrain.basemapDistance = main.basemapDistance;
            terrain.heightmapPixelError = FarRingPixelError;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            terrain.groupingID = 1;
            terrain.allowAutoConnect = false;
        }
    }

    // Distance from a point outside the surround block to the block, which the far ring sinks under.
    static float SurroundBlockDistance(float worldX, float worldZ)
    {
        float dx = Mathf.Max(0f, -MapSize - worldX, worldX - 2f * MapSize);
        float dz = Mathf.Max(0f, -MapSize - worldZ, worldZ - 2f * MapSize);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static TerrainData LoadOrCreateSurroundData(string name)
    {
        string path = $"{TerrainFolder}/Surround_{name}.asset";
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if (data != null)
            return data;

        // The same heightmap resolution as the map, so the shared edges line up sample for sample.
        data = new TerrainData
        {
            heightmapResolution = HeightmapResolution,
            alphamapResolution = SurroundAlphamapResolution,
            baseMapResolution = 512,
            size = new Vector3(MapSize, MapMaxHeight, MapSize),
        };
        AssetDatabase.CreateAsset(data, path);
        return data;
    }

    static Terrain SurroundTerrain(Transform root, string name, TerrainData data)
    {
        string objectName = $"Surround {name}";
        Transform existing = root.Find(objectName);
        GameObject terrainObject = existing != null ? existing.gameObject : Terrain.CreateTerrainGameObject(data);
        terrainObject.name = objectName;
        terrainObject.transform.SetParent(root, true);
        Terrain terrain = terrainObject.GetComponent<Terrain>();
        terrain.terrainData = data;
        TerrainCollider collider = terrainObject.GetComponent<TerrainCollider>();
        if (collider != null)
            collider.terrainData = data;
        return terrain;
    }

    static void CopyTerrainSettings(Terrain main, Terrain terrain)
    {
        terrain.materialTemplate = main.materialTemplate;
        terrain.drawInstanced = main.drawInstanced;
        terrain.basemapDistance = main.basemapDistance;
        terrain.heightmapPixelError = SurroundPixelError;
        terrain.treeDistance = SurroundTreeDistance;
        terrain.treeBillboardDistance = main.treeBillboardDistance;
        terrain.treeCrossFadeLength = main.treeCrossFadeLength;
        terrain.treeMaximumFullLODCount = main.treeMaximumFullLODCount;
        terrain.treeLODBiasMultiplier = main.treeLODBiasMultiplier;
        terrain.detailObjectDistance = main.detailObjectDistance;
        terrain.detailObjectDensity = main.detailObjectDensity;
        terrain.shadowCastingMode = main.shadowCastingMode;
        terrain.groupingID = main.groupingID;
        terrain.allowAutoConnect = true;
    }

    // Distance from a point outside the map to the map's square, zero inside.
    static float OutsideMapDistance(float worldX, float worldZ)
    {
        float dx = Mathf.Max(0f, -worldX, worldX - MapSize);
        float dz = Mathf.Max(0f, -worldZ, worldZ - MapSize);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static float SurroundHillHeightAt(float worldX, float worldZ)
    {
        const float offset = 3100f;
        float broad = Mathf.PerlinNoise(worldX / 320f + offset, worldZ / 320f + offset);
        float fine = Mathf.PerlinNoise(worldX / 95f + offset, worldZ / 95f + offset);
        return SurroundBaseHeight + SurroundHillHeight * ((broad - 0.5f) * 1.6f + (fine - 0.5f) * 0.5f);
    }

    static float[,] SurroundHeights(Terrain main, Vector2 origin, int size)
    {
        float spacing = (float)MapSize / (size - 1);
        var heights = new float[size, size];
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                float worldX = origin.x + x * spacing;
                float worldZ = origin.y + z * spacing;

                // The map's own edge height at the nearest point on its border.
                float edgeX = Mathf.Clamp(worldX, 0f, MapSize);
                float edgeZ = Mathf.Clamp(worldZ, 0f, MapSize);
                float edge = main.terrainData.GetInterpolatedHeight(edgeX / MapSize, edgeZ / MapSize);

                float blend = Mathf.SmoothStep(0f, 1f, OutsideMapDistance(worldX, worldZ) / SurroundBlendWidth);
                float height = Mathf.Lerp(edge, SurroundHillHeightAt(worldX, worldZ), blend);
                heights[z, x] = Mathf.Clamp01(height / MapMaxHeight);
            }
        }
        return heights;
    }

    static float[,,] SurroundAlphamaps(Vector2 origin)
    {
        int resolution = SurroundAlphamapResolution;
        float texelSize = (float)MapSize / resolution;
        var alpha = new float[resolution, resolution, GroundLayerCount];
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
                PaintMeadow(alpha, z, x, origin.x + (x + 0.5f) * texelSize, origin.y + (z + 0.5f) * texelSize);
        }
        return alpha;
    }

    // Meadow grass, the same mix as the map's meadows, out to the grass reach.
    static void PaintSurroundGrass(TerrainData data, Vector2 origin)
    {
        ConfigureDetailPrototypes(data);
        data.SetDetailResolution(SurroundDetailResolution, DetailResolutionPerPatch);

        int size = SurroundDetailResolution;
        float spacing = (float)MapSize / size;
        var dry = new float[size, size];
        var burnt = new float[size, size];
        var tall = new float[size, size];
        var near = new bool[size, size];
        var share = new float[size, size];
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                float worldX = origin.x + x * spacing;
                float worldZ = origin.y + z * spacing;
                float outside = OutsideMapDistance(worldX, worldZ);
                near[z, x] = outside < SurroundGrassReach;
                if (!near[z, x])
                    continue;
                share[z, x] = Mathf.Lerp(1f, SurroundGrassFarShare, Mathf.SmoothStep(0f, 1f, outside / SurroundGrassThinning));
                dry[z, x] = MeadowDryness(worldX, worldZ);
                burnt[z, x] = BurntShare(worldX, worldZ);
                tall[z, x] = TallShare(worldX, worldZ);
            }
        }

        for (int type = 0; type < GrassTypes.Length; type++)
        {
            var random = new System.Random(GrassSeed + type + Mathf.RoundToInt(origin.x * 3f + origin.y * 7f));
            var kind = GrassTypes[type];
            bool heavy = HeavyGrassPrefabs.Contains(kind.prefab);
            var layer = new int[size, size];
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!near[z, x])
                        continue;
                    float density = kind.density * share[z, x] * GrassShare(kind.group, dry[z, x], burnt[z, x], tall[z, x], 0f);
                    if (heavy)
                    {
                        float outside = OutsideMapDistance(origin.x + x * spacing, origin.y + z * spacing);
                        density *= Mathf.SmoothStep(1f, 0f, outside / SurroundHeavyGrassReach);
                    }
                    layer[z, x] = Mathf.FloorToInt(density * spacing * spacing + (float)random.NextDouble());
                }
            }
            data.SetDetailLayer(0, 0, type, layer);
        }
    }

    /// <summary>Tree belts and clumps around the map, sorted into the tile each one falls on.</summary>
    static List<PlacedTree>[] PlaceSurroundTrees()
    {
        var random = new System.Random(SurroundTreeSeed);
        var all = new List<PlacedTree>();

        foreach (var belt in SurroundBelts)
        {
            float length = 4f * MapSize + 2f * Mathf.PI * belt.offset;
            int count = Mathf.FloorToInt(length / belt.spacing);
            for (int i = 0; i < count; i++)
            {
                Vector2 point = BeltPoint(i * belt.spacing, belt.offset);
                // Gaps come from noise along the belt, so trees stand in runs rather than evenly.
                float gap = Mathf.PerlinNoise(point.x / 60f + 4100f, point.y / 60f + 4100f);
                if (gap > belt.keep + 0.15f || random.NextDouble() > 0.85)
                    continue;
                point += new Vector2(RandomRange(random, -belt.jitter, belt.jitter), RandomRange(random, -belt.jitter, belt.jitter));
                AddSurroundTree(all, point, random);
            }
        }

        for (int i = 0; i < SurroundClumps; i++)
        {
            Vector2 centre;
            do
            {
                centre = new Vector2(RandomRange(random, -SurroundClumpMax, MapSize + SurroundClumpMax), RandomRange(random, -SurroundClumpMax, MapSize + SurroundClumpMax));
            }
            while (OutsideMapDistance(centre.x, centre.y) < SurroundClumpMin || OutsideMapDistance(centre.x, centre.y) > SurroundClumpMax);

            for (int j = 0; j < SurroundClumpTrees; j++)
            {
                float angle = RandomRange(random, 0f, Mathf.PI * 2f);
                float distance = SurroundClumpRadius * Mathf.Sqrt((float)random.NextDouble());
                AddSurroundTree(all, centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, random);
            }
        }

        // Trees closer than a crown's width to another are dropped.
        var kept = new List<PlacedTree>();
        foreach (PlacedTree tree in all)
        {
            if (OutsideMapDistance(tree.Position.x, tree.Position.y) > 20f && kept.All(other => Vector2.Distance(other.Position, tree.Position) >= TreeSpacing))
                kept.Add(tree);
        }
        MakeDryTrees(kept, false);

        var byTile = new List<PlacedTree>[SurroundTiles.Length];
        for (int i = 0; i < byTile.Length; i++)
            byTile[i] = new List<PlacedTree>();
        foreach (PlacedTree tree in kept)
        {
            int dx = Mathf.FloorToInt(tree.Position.x / MapSize);
            int dz = Mathf.FloorToInt(tree.Position.y / MapSize);
            int index = System.Array.FindIndex(SurroundTiles, tile => tile.dx == dx && tile.dz == dz);
            if (index >= 0)
                byTile[index].Add(tree);
        }
        return byTile;
    }

    static void AddSurroundTree(List<PlacedTree> trees, Vector2 point, System.Random random)
    {
        trees.Add(new PlacedTree
        {
            Position = point,
            Prefab = SurroundTreeMix[random.Next(SurroundTreeMix.Length)],
            Scale = RandomRange(random, MinTreeScale, MaxTreeScale),
        });
    }

    // A point at a distance along a loop around the map, at a fixed offset outside it with rounded corners.
    static Vector2 BeltPoint(float along, float offset)
    {
        float arc = Mathf.PI * 0.5f * offset;
        float side = MapSize + arc;
        int segment = Mathf.FloorToInt(along / side) % 4;
        float t = along - segment * side;

        // Each side runs anticlockwise from the south, followed by the corner after it.
        Vector2[] starts = { new Vector2(0, -offset), new Vector2(MapSize + offset, 0), new Vector2(MapSize, MapSize + offset), new Vector2(-offset, MapSize) };
        Vector2[] directions = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
        Vector2[] corners = { new Vector2(MapSize, 0), new Vector2(MapSize, MapSize), new Vector2(0, MapSize), new Vector2(0, 0) };
        if (t < MapSize)
            return starts[segment] + directions[segment] * t;

        float angle = -Mathf.PI * 0.5f + segment * Mathf.PI * 0.5f + (t - MapSize) / offset;
        return corners[segment] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * offset;
    }

    static void ApplySurroundTrees(TerrainData data, List<PlacedTree> trees, Vector2 origin)
    {
        // Old instances would be checked against the new prototype list and reported as invalid.
        data.SetTreeInstances(new TreeInstance[0], false);
        List<string> prefabs = SurroundTreeMix.Concat(SurroundTreeMix.Select(name => DryTreeCopies[name].prefab))
            .Distinct().OrderBy(name => name, System.StringComparer.Ordinal).ToList();
        data.treePrototypes = prefabs
            .Select(name => new TreePrototype { prefab = LoadRequired<GameObject>($"{TreesFolder}/{name}.prefab") })
            .ToArray();

        var random = new System.Random(SurroundTreeSeed + Mathf.RoundToInt(origin.x * 3f + origin.y * 7f));
        data.SetTreeInstances(trees.Select(tree => new TreeInstance
        {
            position = new Vector3((tree.Position.x - origin.x) / MapSize, 0f, (tree.Position.y - origin.y) / MapSize),
            prototypeIndex = prefabs.IndexOf(tree.Prefab),
            heightScale = tree.Scale,
            widthScale = tree.Scale * RandomRange(random, 1f - TreeWidthVariation, 1f + TreeWidthVariation),
            rotation = RandomRange(random, 0f, Mathf.PI * 2f),
            color = Color.white,
            lightmapColor = Color.white,
        }).ToArray(), true);
    }
}
