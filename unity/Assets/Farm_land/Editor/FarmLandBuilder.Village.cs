using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Step 8: a small abandoned village of ruined houses along a grassy lane, in the meadow between F6 and the
/// central crossroads. The ruins come from Models/build_village_ruins.py, which runs in Blender. The
/// houses' ground is levelled and painted in Step 4, and grass and trees keep clear of them in Steps 5 and 7.
/// </summary>
public static partial class FarmLandBuilder
{
    const string VillageFolder = "Assets/Farm_land/Prefabs/Village";
    const string RuinsModelPath = ModelsFolder + "/village_ruins.fbx";
    const string HousePackFolder = "Assets/ALP_Assets/country house01";
    const string HouseDoorsModelPath = HousePackFolder + "/Models/Doors.fbx";
    const string VillageObjectName = "Village";
    const int VillageSeed = 8120;

    // The house in the model, in local metres: the porch faces +Z, and the back wall is at -5.1.
    static readonly Rect HouseFootprint = Rect.MinMaxRect(-7.4f, -5.1f, 7.4f, 6.9f);

    // Level ground just past the walls, easing back into the terrain over the shoulder.
    const float HouseGroundMargin = 1.5f;
    const float HouseGroundShoulder = 7f;
    const float HouseSink = 0.08f;

    // Rubble ground around the walls, and how far grass and trunks keep from them.
    const float HouseRubbleReach = 3.5f;
    const float HouseGrassClearance = 0.3f;
    const float HouseTreeClearance = 3f;

    // Checked at build time and reported, in metres from the house outline.
    const float HouseFieldClearance = 6f;
    const float HouseTrackClearance = 3f;

    // Trees near the houses rank as drier, so most of them die back.
    static readonly Vector2 VillageCentre = new Vector2(512f, 600f);
    const float VillageRadius = 75f;
    const float VillageDryBoost = 0.35f;

    const int DebrisPerHouseMin = 4;
    const int DebrisPerHouseMax = 7;
    const float DebrisReach = 4.5f;
    const float DebrisMaxTilt = 6f;
    const float DebrisCullScreenHeight = 0.004f;

    /// <summary>Ruined houses: the ruin mesh, centre, yaw in degrees (0 has the porch facing north), and whether it's mirrored.</summary>
    static readonly (string ruin, float x, float z, float yaw, bool mirror)[] VillageHouses =
    {
        ("Ruin_RoofCollapsed", 539f, 616.5f, 186f, false),
        ("Ruin_Burnt", 511f, 616f, 176f, false),
        ("Ruin_Walls", 478f, 618f, 183f, true),
        ("Ruin_Stump", 536f, 586f, -5f, false),
        ("Ruin_Walls", 500f, 585f, 4f, false),
    };

    static readonly string[] RuinMeshes = { "Ruin_RoofCollapsed", "Ruin_Walls", "Ruin_Burnt", "Ruin_Stump" };
    static readonly string[] RuinDebrisMeshes = { "Debris_RoofSheet_A", "Debris_RoofSheet_B", "Debris_WallPanel", "Debris_Window" };
    static readonly string[] DoorMeshes = { "Door01", "Door02", "Door03", "Door04" };

    // Debris prefab names, weighted by repeating them. Doors are the pack's doors lying flat.
    static readonly string[] DebrisChoices =
    {
        "Debris_RoofSheet_A", "Debris_RoofSheet_B", "Debris_RoofSheet_A", "Debris_WallPanel", "Debris_WallPanel",
        "Debris_Window", "Door01", "Door02", "Door03", "Door04",
    };

    // House materials: the pack's base, normal and occlusion maps, and smoothness, since its gloss values are
    // for a different workflow. Charred copies darken the same textures.
    static readonly (string source, string name, string baseMap, string normalMap, string occlusionMap, float smoothness)[] HouseMaterials =
    {
        ("OldHouseMapIndoorWall01", "House_IndoorWall", "OldHouseMapIndoorWall01.png", "OldHouseMapIndoorWall01_N.png", null, 0.15f),
        ("OldHouseMapWood01", "House_Wood01", "OldHouseMapWood01.png", "OldHouseMapWood01_N.png", "OldHouseMapWood01_AO.png", 0.1f),
        ("OldHouseMapWood02", "House_Wood02", "OldHouseMapWood02.png", "OldHouseMapWood02_N.png", null, 0.12f),
        ("WindowsMat01", "House_Windows", "WindowMap01.png", "WindowMap01_N.png", null, 0.15f),
        ("DoorsMap01", "House_Doors", "DoorsMap01.png", "DoorsMap01_N.png", "DoorsMap01_AO.png", 0.08f),
    };

    static readonly Color BurntTint = new Color(0.5f, 0.46f, 0.43f);
    const float BurntSmoothness = 0.04f;

    [MenuItem("Farm Land/Step 8 - Abandoned Village")]
    public static void BuildAbandonedVillage()
    {
        RequireEditMode();
        BuildVillageAssets();

        // The lane and the house plots are part of the terrain build, so grass and trees follow it.
        BuildDirtTracks();
        BuildGrass();
        BuildPlaceTrees();
        PlaceVillage();
    }

    /// <summary>Only re-places the houses and debris, for when nothing on the terrain has changed.</summary>
    [MenuItem("Farm Land/Step 8 - Village Objects Only")]
    public static void PlaceVillage()
    {
        RequireEditMode();
        Scene scene = OpenFarmLandScene();
        Terrain terrain = GameObject.Find(TerrainObjectName).GetComponent<Terrain>();

        GameObject existing = GameObject.Find(VillageObjectName);
        if (existing != null)
            Object.DestroyImmediate(existing);
        var root = new GameObject(VillageObjectName);

        var random = new System.Random(VillageSeed);
        int debrisCount = 0;
        foreach (var house in VillageHouses)
        {
            var prefab = LoadRequired<GameObject>($"{VillageFolder}/{house.ruin}.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            float ground = terrain.SampleHeight(new Vector3(house.x, 0f, house.z)) + terrain.transform.position.y;
            instance.transform.SetPositionAndRotation(new Vector3(house.x, ground - HouseSink, house.z), Quaternion.Euler(0f, house.yaw, 0f));
            instance.transform.localScale = new Vector3(house.mirror ? -1f : 1f, 1f, 1f);
            debrisCount += PlaceDebris(root.transform, terrain, house, random);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(DescribeVillage(debrisCount));
    }

    static int PlaceDebris(Transform parent, Terrain terrain, (string ruin, float x, float z, float yaw, bool mirror) house, System.Random random)
    {
        int count = random.Next(DebrisPerHouseMin, DebrisPerHouseMax + 1);
        int placed = 0;
        for (int attempt = 0; attempt < count * 20 && placed < count; attempt++)
        {
            // A spot in the ring just outside the walls, kept off the lane and out of the fields.
            Vector2 local = new Vector2(
                RandomRange(random, HouseFootprint.xMin - DebrisReach, HouseFootprint.xMax + DebrisReach),
                RandomRange(random, HouseFootprint.yMin - DebrisReach, HouseFootprint.yMax + DebrisReach));
            Vector2 spot = HouseToWorld(house, local);
            float edge = HouseEdgeDistance(house, spot);
            if (edge < 0.4f || NearestTrackEdge(spot) < 0.5f || NearestFieldEdge(spot) < 1f)
                continue;

            string name = DebrisChoices[random.Next(DebrisChoices.Length)];
            var prefab = LoadRequired<GameObject>($"{VillageFolder}/{name}.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);

            var world = new Vector3(spot.x, 0f, spot.y);
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y - 0.02f;
            Vector3 normal = terrain.terrainData.GetInterpolatedNormal(spot.x / MapSize, spot.y / MapSize);
            Quaternion tilt = Quaternion.Euler(RandomRange(random, -DebrisMaxTilt, DebrisMaxTilt), 0f, RandomRange(random, -DebrisMaxTilt, DebrisMaxTilt));
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f) * tilt;
            instance.transform.SetPositionAndRotation(world, rotation);
            placed++;
        }
        return placed;
    }

    /// <summary>Builds the house materials and the ruin, debris and door prefabs.</summary>
    public static void BuildVillageAssets()
    {
        EnsureFolder("Assets/Farm_land/Prefabs");
        EnsureFolder(VillageFolder);
        EnsureFolder(UrpMaterialsFolder);
        ConfigureVillageModel();

        var materials = new Dictionary<string, Material>();
        foreach (var entry in HouseMaterials)
        {
            materials[entry.source] = BuildHouseMaterial(entry.name, entry, Color.white, entry.smoothness);
            materials[entry.source + "_Burnt"] = BuildHouseMaterial(entry.name + "_Burnt", entry, BurntTint, BurntSmoothness);
        }

        GameObject ruins = LoadRequired<GameObject>(RuinsModelPath);
        foreach (string name in RuinMeshes)
            SaveVillagePrefab(name, FindModelPart(ruins, name, RuinsModelPath), materials, Quaternion.identity, false);
        foreach (string name in RuinDebrisMeshes)
            SaveVillagePrefab(name, FindModelPart(ruins, name, RuinsModelPath), materials, Quaternion.identity, true);

        // The pack's doors stand upright with their thin side along X, so they're laid on their side.
        GameObject doors = LoadRequired<GameObject>(HouseDoorsModelPath);
        foreach (string name in DoorMeshes)
            SaveVillagePrefab(name, FindModelPart(doors, name, HouseDoorsModelPath), materials, Quaternion.Euler(0f, 0f, 90f), true);

        AssetDatabase.SaveAssets();
    }

    static void ConfigureVillageModel()
    {
        AssetDatabase.ImportAsset(RuinsModelPath, ImportAssetOptions.ForceSynchronousImport);
        var model = (ModelImporter)AssetImporter.GetAtPath(RuinsModelPath);
        model.importNormals = ModelImporterNormals.Import;
        model.importTangents = ModelImporterTangents.CalculateMikk;
        model.importCameras = false;
        model.importLights = false;
        model.importAnimation = false;
        model.animationType = ModelImporterAnimationType.None;
        model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        model.meshCompression = ModelImporterMeshCompression.Off;
        model.isReadable = false;
        model.SaveAndReimport();

        // Occlusion maps hold data, not colour.
        foreach (var entry in HouseMaterials.Where(entry => entry.occlusionMap != null))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath($"{HousePackFolder}/Textures/{entry.occlusionMap}");
            if (importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }
        }
    }

    static Transform FindModelPart(GameObject model, string name, string path)
    {
        Transform part = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == name);
        if (part == null)
            throw new System.IO.FileNotFoundException($"FarmLandBuilder: {name} is missing from the model", path);
        return part;
    }

    // Broken walls are open shells, so both sides of every face draw.
    static Material BuildHouseMaterial(string name, (string source, string name, string baseMap, string normalMap, string occlusionMap, float smoothness) entry, Color tint, float smoothness)
    {
        string textures = $"{HousePackFolder}/Textures";
        Material material = BuildWoodMaterial(
            $"{UrpMaterialsFolder}/{name}.mat",
            LoadRequired<Texture>($"{textures}/{entry.baseMap}"),
            LoadRequired<Texture>($"{textures}/{entry.normalMap}"),
            entry.occlusionMap != null ? LoadRequired<Texture>($"{textures}/{entry.occlusionMap}") : null,
            1f,
            smoothness);
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    // Debris is re-centred so its prefab origin sits on the ground under the middle of the piece.
    static void SaveVillagePrefab(string name, Transform part, Dictionary<string, Material> materials, Quaternion rotation, bool debris)
    {
        var root = new GameObject(name);
        var child = new GameObject(name + "_Mesh");
        child.transform.SetParent(root.transform, false);
        child.transform.localRotation = rotation;
        child.AddComponent<MeshFilter>().sharedMesh = part.GetComponent<MeshFilter>().sharedMesh;

        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = part.GetComponent<MeshRenderer>().sharedMaterials
            .Select(slot => materials.TryGetValue(slot.name, out Material material)
                ? material
                : throw new KeyNotFoundException($"FarmLandBuilder: no house material for {slot.name} on {name}"))
            .ToArray();
        renderer.shadowCastingMode = ShadowCastingMode.On;

        if (debris)
        {
            Bounds bounds = renderer.bounds;
            child.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

            // Small pieces disappear well before they'd shrink to a few pixels.
            LODGroup group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(DebrisCullScreenHeight, new Renderer[] { renderer }) });
            group.RecalculateBounds();
        }

        PrefabUtility.SaveAsPrefabAsset(root, $"{VillageFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
    }

    static Vector2 HouseToWorld((string ruin, float x, float z, float yaw, bool mirror) house, Vector2 local)
    {
        if (house.mirror)
            local.x = -local.x;
        float angle = house.yaw * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        return new Vector2(house.x + local.x * cos + local.y * sin, house.z - local.x * sin + local.y * cos);
    }

    static Vector2 WorldToHouse((string ruin, float x, float z, float yaw, bool mirror) house, Vector2 world)
    {
        float angle = house.yaw * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        float dx = world.x - house.x;
        float dz = world.y - house.z;
        var local = new Vector2(dx * cos - dz * sin, dx * sin + dz * cos);
        if (house.mirror)
            local.x = -local.x;
        return local;
    }

    // Distance from the house outline, negative inside.
    static float HouseEdgeDistance((string ruin, float x, float z, float yaw, bool mirror) house, Vector2 world)
    {
        Vector2 local = WorldToHouse(house, world);
        Vector2 half = HouseFootprint.size * 0.5f;
        Vector2 offset = local - HouseFootprint.center;
        var outside = new Vector2(Mathf.Abs(offset.x) - half.x, Mathf.Abs(offset.y) - half.y);
        return Vector2.Max(outside, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(outside.x, outside.y), 0f);
    }

    static float NearestHouseEdge(Vector2 world)
    {
        float nearest = float.MaxValue;
        foreach (var house in VillageHouses)
            nearest = Mathf.Min(nearest, HouseEdgeDistance(house, world));
        return nearest;
    }

    static bool InVillage(Vector2 world)
    {
        return Vector2.Distance(world, VillageCentre) < VillageRadius;
    }

    /// <summary>Levels the ground under each house to its average height, easing back into the terrain. Part of Step 4.</summary>
    static void LevelVillageGround(TerrainData data)
    {
        int size = data.heightmapResolution;
        float spacing = (float)MapSize / (size - 1);
        float[,] heights = data.GetHeights(0, 0, size, size);
        float reach = HouseGroundMargin + HouseGroundShoulder;

        foreach (var house in VillageHouses)
        {
            GetHouseSampleRange(house, reach, spacing, size, out int minX, out int maxX, out int minZ, out int maxZ);

            float total = 0f;
            int count = 0;
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (HouseEdgeDistance(house, new Vector2(x * spacing, z * spacing)) <= 0f)
                    {
                        total += heights[z, x];
                        count++;
                    }
                }
            }
            float level = total / Mathf.Max(1, count);

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float edge = HouseEdgeDistance(house, new Vector2(x * spacing, z * spacing));
                    float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(reach, HouseGroundMargin, edge));
                    heights[z, x] = Mathf.Lerp(heights[z, x], level, weight);
                }
            }
        }
        data.SetHeights(0, 0, heights);
    }

    /// <summary>Paints trampled earth and rubble around and under the houses, fading into the meadow. Part of Step 4.</summary>
    static void PaintVillageGround(TerrainData data)
    {
        int resolution = data.alphamapResolution;
        float texelSize = (float)MapSize / resolution;
        float[,,] alpha = data.GetAlphamaps(0, 0, resolution, resolution);

        foreach (var house in VillageHouses)
        {
            GetHouseSampleRange(house, HouseRubbleReach + 2f, texelSize, resolution, out int minX, out int maxX, out int minZ, out int maxZ);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float worldX = x * texelSize;
                    float worldZ = z * texelSize;
                    float uneven = (BlendNoise(worldX, worldZ, 2100f) - 0.5f) * 3f;
                    float edge = HouseEdgeDistance(house, new Vector2(worldX, worldZ)) + uneven;
                    float coverage = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HouseRubbleReach, 0.5f, edge));
                    if (coverage <= 0f)
                        continue;

                    for (int i = 0; i < GroundLayerCount; i++)
                        alpha[z, x, i] *= 1f - coverage;

                    float rubble = 0.35f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.7f, BlendNoise(worldX, worldZ, 2300f)));
                    float weeds = 0.3f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, BlendNoise(worldX, worldZ, 2500f)));
                    alpha[z, x, (int)GroundLayer.Gravel] += coverage * rubble;
                    alpha[z, x, (int)GroundLayer.GrassDry] += coverage * weeds;
                    alpha[z, x, (int)GroundLayer.DryMud] += coverage * (1f - rubble - weeds);
                }
            }
        }
        data.SetAlphamaps(0, 0, alpha);
    }

    /// <summary>True for grass samples under a house, which would poke up through its floors. Part of Step 5.</summary>
    static bool[,] HouseGrassMask(int size, float spacing)
    {
        var mask = new bool[size, size];
        foreach (var house in VillageHouses)
        {
            GetHouseSampleRange(house, HouseGrassClearance, spacing, size, out int minX, out int maxX, out int minZ, out int maxZ);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (HouseEdgeDistance(house, new Vector2(x * spacing, z * spacing)) <= HouseGrassClearance)
                        mask[z, x] = true;
                }
            }
        }
        return mask;
    }

    static void GetHouseSampleRange((string ruin, float x, float z, float yaw, bool mirror) house, float reach, float spacing, int size,
        out int minX, out int maxX, out int minZ, out int maxZ)
    {
        float radius = HouseFootprint.size.magnitude * 0.5f + HouseFootprint.center.magnitude + reach;
        minX = Mathf.Max(0, Mathf.FloorToInt((house.x - radius) / spacing));
        maxX = Mathf.Min(size - 1, Mathf.CeilToInt((house.x + radius) / spacing));
        minZ = Mathf.Max(0, Mathf.FloorToInt((house.z - radius) / spacing));
        maxZ = Mathf.Min(size - 1, Mathf.CeilToInt((house.z + radius) / spacing));
    }

    static string DescribeVillage(int debrisCount)
    {
        var report = new StringBuilder("House | ruin | triangles | clearance to a field, a track, another house (m)\n");
        GameObject root = GameObject.Find(VillageObjectName);
        int total = 0;
        foreach (var house in VillageHouses)
        {
            float field = float.MaxValue;
            float track = float.MaxValue;
            float other = float.MaxValue;
            for (int i = 0; i <= 40; i++)
            {
                // Walk the outline and measure from each point on it.
                float t = i / 40f;
                Vector2 min = HouseFootprint.min;
                Vector2 max = HouseFootprint.max;
                foreach (Vector2 local in new[]
                {
                    new Vector2(Mathf.Lerp(min.x, max.x, t), min.y), new Vector2(Mathf.Lerp(min.x, max.x, t), max.y),
                    new Vector2(min.x, Mathf.Lerp(min.y, max.y, t)), new Vector2(max.x, Mathf.Lerp(min.y, max.y, t)),
                })
                {
                    Vector2 world = HouseToWorld(house, local);
                    field = Mathf.Min(field, NearestFieldEdge(world));
                    track = Mathf.Min(track, NearestTrackEdge(world));
                    foreach (var neighbour in VillageHouses.Where(n => n != house))
                        other = Mathf.Min(other, HouseEdgeDistance(neighbour, world));
                }
            }
            string warning = field < HouseFieldClearance || track < HouseTrackClearance ? "  TOO CLOSE" : "";
            int triangles = LoadRequired<GameObject>($"{VillageFolder}/{house.ruin}.prefab").GetComponentInChildren<MeshFilter>().sharedMesh.triangles.Length / 3;
            total += triangles;
            report.AppendLine($"({house.x:F0}, {house.z:F0}) | {house.ruin}{(house.mirror ? " mirrored" : "")} | {triangles} | {field:F1}, {track:F1}, {other:F1}{warning}");
        }
        report.AppendLine($"{VillageHouses.Length} houses, {total} triangles, {debrisCount} pieces of debris, {root.transform.childCount} objects");
        return report.ToString().TrimEnd();
    }
}
