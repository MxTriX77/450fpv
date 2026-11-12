using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Step 5: terrain detail grass, using generated copies of the sample prefabs. Only the detail maps
/// change on the terrain, so this can be re-run on its own.
/// </summary>
public static partial class FarmLandBuilder
{
    const string SampleGrassFolder = "Assets/TerrainSampleAssets/Prefabs";
    const string GrassFolder = "Assets/Farm_land/Prefabs/Grass";
    const string UrpMaterialsFolder = "Assets/Farm_land/URP_Materials";
    const string GrassShaderName = "Farm Land/Grass";
    const int GrassSeed = 5309;

    const int DetailResolution = 1024;
    const int DetailResolutionPerPatch = 32;
    const float GrassDrawDistance = 200f;

    // Ends just inside the draw distance, so instances are fully faded before their patch is culled.
    const float GrassFadeStart = 150f;
    const float GrassFadeEnd = 195f;

    // How far blade normals bend towards the sky, and how dark the base of a clump gets. Less bending lets
    // blades turned from the sun fall darker, and the darker base stands in for blades shading each other,
    // so midday meadows don't read pale and flat (owner feedback at Step 9).
    const float GrassNormalUp = 0.6f;
    const float GrassBaseShade = 0.4f;
    const float GrassShadeHeight = 0.8f;

    // Bare gaps beside soil and tracks, in metres. The band is where grass grows thicker along fields.
    const float GrassFieldClearance = 0.2f;
    const float GrassTrackClearance = 0.3f;
    const float GrassTrackFadeWidth = 1.2f;
    const float GrassFieldBand = 6f;

    // Weeds on fields are mostly dry, and overgrown fields grow more of them tall.
    const float FieldWeedDryness = 0.7f;
    const float FieldTallShare = 0.6f;

    // Bleaches the dry grass textures to a pale, washed-out straw. Values above 1 brighten, and warmer
    // values than this quickly read as ripe wheat.
    static readonly Color BurntGrassTint = new Color(1.12f, 1.1f, 1.02f);

    enum GrassGroup
    {
        Green,
        GreenClump,
        Dry,
        DryClump,
        Burnt,
        TallGreen,
        TallDry,
        TallBurnt,
    }

    // Each row is one detail layer, and tall grass reuses cheap meshes stretched upward. The clumps cost
    // about 1,100 triangles each and the rest under 200, so clumps stay sparse. Scales are multiples of the
    // mesh size. Density is instances per square metre where the group is at full share, and weeds is the
    // same inside fields where the weed share is full.
    static readonly (string prefab, string sample, GrassGroup group, float density, float weeds, float minWidth, float maxWidth, float minHeight, float maxHeight)[] GrassTypes =
    {
        ("Grass_A", "Grass_A", GrassGroup.Green, 3.5f, 1.7f, 2.6f, 3.4f, 1.8f, 2.6f),
        ("Grass_B", "Grass_B", GrassGroup.GreenClump, 0.05f, 0.07f, 1.2f, 1.7f, 1.3f, 1.8f),
        ("Grass_C", "Grass_C", GrassGroup.GreenClump, 0.05f, 0.07f, 1.0f, 1.4f, 1.0f, 1.4f),
        ("Grass_D", "Grass_D", GrassGroup.GreenClump, 0.05f, 0.07f, 0.9f, 1.3f, 0.9f, 1.3f),
        ("GrassDry_A", "GrassDry_A", GrassGroup.Dry, 1.6f, 1.7f, 1.2f, 1.7f, 1.2f, 1.7f),
        ("GrassDry_B", "GrassDry_B", GrassGroup.Dry, 1.6f, 1.7f, 1.6f, 2.2f, 1.6f, 2.2f),
        ("GrassDry_C", "GrassDry_C", GrassGroup.DryClump, 0.4f, 0.43f, 1.0f, 1.4f, 1.0f, 1.4f),
        ("GrassBurnt_A", "GrassDry_A", GrassGroup.Burnt, 2.2f, 1.6f, 1.2f, 1.7f, 1.0f, 1.5f),
        ("GrassBurnt_B", "GrassDry_B", GrassGroup.Burnt, 2.2f, 1.6f, 1.6f, 2.2f, 1.2f, 1.8f),
        ("GrassBurnt_C", "GrassDry_C", GrassGroup.Burnt, 0.5f, 0.4f, 1.0f, 1.6f, 1.0f, 1.7f),
        ("Grass_A", "Grass_A", GrassGroup.TallGreen, 1.0f, 1.0f, 2.6f, 3.6f, 3.6f, 5.2f),
        ("GrassDry_C", "GrassDry_C", GrassGroup.TallDry, 0.6f, 0.6f, 1.5f, 2.2f, 1.8f, 2.6f),
        ("GrassDry_A", "GrassDry_A", GrassGroup.TallDry, 0.5f, 0.5f, 1.8f, 2.6f, 2.2f, 3.0f),
        ("GrassBurnt_C", "GrassDry_C", GrassGroup.TallBurnt, 0.5f, 0.5f, 1.5f, 2.2f, 1.8f, 2.6f),
    };

    [MenuItem("Farm Land/Step 5 - Grass")]
    public static void BuildGrass()
    {
        RequireEditMode();

        BuildGrassPrefabs();

        TerrainData data = LoadRequired<TerrainData>(TerrainDataPath);
        ConfigureDetailPrototypes(data);
        PaintGrass(data);
        EditorUtility.SetDirty(data);

        Terrain terrain = GameObject.Find(TerrainObjectName).GetComponent<Terrain>();
        terrain.detailObjectDistance = GrassDrawDistance;
        terrain.detailObjectDensity = 1f;
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        EditorSceneManager.SaveScene(terrain.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    /// <summary>Creates or updates grass prefabs and materials under Assets/Farm_land, using the sample meshes and textures.</summary>
    public static void BuildGrassPrefabs()
    {
        EnsureFolder("Assets/Farm_land/Prefabs");
        EnsureFolder(GrassFolder);
        EnsureFolder(UrpMaterialsFolder);

        var built = new HashSet<string>();
        foreach (var type in GrassTypes)
        {
            if (!built.Add(type.prefab))
                continue;

            GameObject sample = LoadRequired<GameObject>($"{SampleGrassFolder}/{type.sample}.prefab");
            Material source = sample.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            bool burnt = type.group == GrassGroup.Burnt || type.group == GrassGroup.TallBurnt;

            var instance = new GameObject(type.prefab);
            instance.AddComponent<MeshFilter>().sharedMesh = sample.GetComponentInChildren<MeshFilter>().sharedMesh;
            MeshRenderer renderer = instance.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = burnt
                ? BuildGrassMaterial(source, "GrassBurnt", BurntGrassTint)
                : BuildGrassMaterial(source, source.name, Color.white);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(instance, $"{GrassFolder}/{type.prefab}.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
        }
        AssetDatabase.SaveAssets();
    }

    // Reads textures and settings from the sample material, whose property names come from its shader graph.
    static Material BuildGrassMaterial(Material source, string name, Color tint)
    {
        const string baseMap = "Texture2D_E1B0D043";
        const string maskMap = "Texture2D_A5E0646";
        const string occlusionScale = "Vector1_593c5cea6c4a42e993ed03ced4685732";
        const string alphaClip = "Vector1_a5b8b09028ce49a39f4d090894c89e22";

        string path = $"{UrpMaterialsFolder}/{name}.mat";
        Shader shader = Shader.Find(GrassShaderName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetTexture("_BaseMap", source.GetTexture(baseMap));
        material.SetTexture("_MaskMap", source.GetTexture(maskMap));
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Cutoff", source.GetFloat(alphaClip));
        material.SetFloat("_OcclusionStrength", source.GetFloat(occlusionScale));
        material.SetFloat("_FadeStart", GrassFadeStart);
        material.SetFloat("_FadeEnd", GrassFadeEnd);
        material.SetFloat("_NormalUp", GrassNormalUp);
        material.SetFloat("_BaseShade", GrassBaseShade);
        material.SetFloat("_ShadeHeight", GrassShadeHeight);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ConfigureDetailPrototypes(TerrainData data)
    {
        var prototypes = new DetailPrototype[GrassTypes.Length];
        for (int i = 0; i < prototypes.Length; i++)
        {
            var type = GrassTypes[i];
            prototypes[i] = new DetailPrototype
            {
                prototype = LoadRequired<GameObject>($"{GrassFolder}/{type.prefab}.prefab"),
                usePrototypeMesh = true,
                useInstancing = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = type.minWidth,
                maxWidth = type.maxWidth,
                minHeight = type.minHeight,
                maxHeight = type.maxHeight,
                noiseSpread = 0.1f,
                healthyColor = Color.white,
                dryColor = Color.white,
                alignToGround = 0.5f,
                positionJitter = 100f,
            };
        }

        data.detailPrototypes = prototypes;
        data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        data.SetDetailResolution(DetailResolution, DetailResolutionPerPatch);
    }

    static void PaintGrass(TerrainData data)
    {
        float spacing = (float)MapSize / DetailResolution;
        float[,] fieldDistance = NearestFieldDistance(DetailResolution, spacing, GrassFieldBand + FieldEdgeWobble + FieldEdgeFineWobble);
        float[,] weeds = FieldWeedShares(DetailResolution, spacing, out float[,] overgrowth);
        float[,] trackDistance = TrackEdgeDistance(null, DetailResolution, spacing, GrassTrackClearance + GrassTrackFadeWidth + TrackEdgeReach);
        bool[,] houses = HouseGrassMask(DetailResolution, spacing);

        for (int type = 0; type < GrassTypes.Length; type++)
        {
            var random = new System.Random(GrassSeed + type);
            var layer = new int[DetailResolution, DetailResolution];
            for (int z = 0; z < DetailResolution; z++)
            {
                for (int x = 0; x < DetailResolution; x++)
                {
                    float worldX = x * spacing;
                    float worldZ = z * spacing;
                    float wobble = FieldEdgeOffset(worldX, worldZ);
                    float field = fieldDistance[z, x] + wobble;
                    float track = trackDistance[z, x] + wobble * TrackEdgeWobbleShare;
                    if (track <= GrassTrackClearance || houses[z, x])
                        continue;

                    var kind = GrassTypes[type];
                    float burnt = BurntShare(worldX, worldZ);
                    float tall = TallShare(worldX, worldZ);
                    float density;
                    if (field <= GrassFieldClearance)
                    {
                        float fieldDry = Mathf.Max(FieldWeedDryness, MeadowDryness(worldX, worldZ));
                        float fieldTall = Mathf.Max(tall, overgrowth[z, x] * overgrowth[z, x] * FieldTallShare);
                        density = kind.weeds * weeds[z, x] * GrassShare(kind.group, fieldDry, burnt, fieldTall, 0f);
                    }
                    else
                    {
                        float trackFade = Mathf.InverseLerp(GrassTrackClearance, GrassTrackClearance + GrassTrackFadeWidth, track);
                        float edge = Mathf.InverseLerp(GrassFieldBand, GrassFieldClearance, field);
                        density = kind.density * trackFade * GrassShare(kind.group, MeadowDryness(worldX, worldZ), burnt, tall, edge);
                    }

                    // The fraction becomes a chance of one more instance, so low densities still scatter.
                    layer[z, x] = Mathf.FloorToInt(density * spacing * spacing + (float)random.NextDouble());
                }
            }
            data.SetDetailLayer(0, 0, type, layer);
        }
    }

    // How much of a group grows at a point, from the shares of dry, burnt and tall grass there.
    static float GrassShare(GrassGroup group, float dry, float burnt, float tall, float edge)
    {
        float unburnt = 1f - burnt;
        switch (group)
        {
            case GrassGroup.Green: return unburnt * (1f - 0.75f * dry) * (1f + edge * 0.5f);
            case GrassGroup.GreenClump: return unburnt * (1f - dry) * (1f + edge * 4f);
            case GrassGroup.Dry: return unburnt * dry;
            case GrassGroup.DryClump: return unburnt * dry * (1f + edge * 2f);
            case GrassGroup.Burnt: return burnt;
            case GrassGroup.TallGreen: return unburnt * tall * (1f - 0.7f * dry);
            case GrassGroup.TallDry: return unburnt * tall * (0.25f + 0.75f * dry);
            default: return burnt * tall;
        }
    }

    // Patches of tall grass mixed in with the rest, about 65 m across with uneven edges.
    static float TallShare(float worldX, float worldZ)
    {
        const float offset = 510f;
        float broad = Mathf.PerlinNoise(worldX / 65f + offset, worldZ / 65f + offset);
        float fine = Mathf.PerlinNoise(worldX / 18f + offset, worldZ / 18f + offset);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.54f, 0.68f, broad * 0.7f + fine * 0.3f));
    }

    // Weed share at every sample in or just outside a field, since field edges wobble past their outline.
    static float[,] FieldWeedShares(int size, float spacing, out float[,] overgrowth)
    {
        float reach = FieldEdgeWobble + FieldEdgeFineWobble + GrassFieldClearance;
        var shares = new float[size, size];
        overgrowth = new float[size, size];
        foreach (FarmField field in Fields)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt((field.Bounds.xMin - reach) / spacing));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt((field.Bounds.xMax + reach) / spacing));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((field.Bounds.yMin - reach) / spacing));
            int maxZ = Mathf.Min(size - 1, Mathf.CeilToInt((field.Bounds.yMax + reach) / spacing));

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float worldX = x * spacing;
                    float worldZ = z * spacing;
                    float distance = field.SignedDistance(new Vector2(worldX, worldZ));
                    if (distance > reach)
                        continue;
                    shares[z, x] = field.WeedShare(worldX, worldZ, distance);
                    overgrowth[z, x] = field.Overgrowth;
                }
            }
        }
        return shares;
    }

    // Signed distance to the nearest field, infinite past the reach.
    static float[,] NearestFieldDistance(int size, float spacing, float reach)
    {
        var distances = new float[size, size];
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
                distances[z, x] = float.PositiveInfinity;
        }

        foreach (FarmField field in Fields)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt((field.Bounds.xMin - reach) / spacing));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt((field.Bounds.xMax + reach) / spacing));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((field.Bounds.yMin - reach) / spacing));
            int maxZ = Mathf.Min(size - 1, Mathf.CeilToInt((field.Bounds.yMax + reach) / spacing));

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = field.SignedDistance(new Vector2(x * spacing, z * spacing));
                    if (distance <= reach && distance < distances[z, x])
                        distances[z, x] = distance;
                }
            }
        }
        return distances;
    }
}
