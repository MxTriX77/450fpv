using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Step 2: import settings, generated textures, terrain layers and a test terrain.
/// </summary>
public static partial class FarmLandBuilder
{
    const string TerrainFolder = "Assets/Farm_land/Terrain";
    const string LayersFolder = TerrainFolder + "/Layers";
    const string GeneratedFolder = TerrainFolder + "/Generated";
    const string SourceTexturesRoot = "Assets/Farm_land/Materials";
    const string SampleLayersFolder = "Assets/TerrainSampleAssets/TerrainLayers";
    const string TerrainDataPath = TerrainFolder + "/FarmLand_TerrainData.asset";
    const string TerrainMaterialPath = TerrainFolder + "/FarmLand_TerrainLit.mat";
    const string TerrainObjectName = "Terrain";

    const string AerialMud = "aerial_mud_1";
    const string DryMud = "dry_mud_field_001";
    const string GrassGround = "grass_ground";
    const string GrassPath = "grass_path_3";
    const string Gravel = "gravel_ground_01";

    static readonly string[] SourceTextureSets = { AerialMud, DryMud, GrassGround, GrassPath, Gravel };

    const int MapSize = 1000;
    const int MapMaxHeight = 120;
    const int HeightmapResolution = 1025;
    const int AlphamapResolution = 2048;
    const int GeneratedTextureSize = 2048;
    const int GroundAnisoLevel = 8;
    const float MudTileSize = 11f;

    /// <summary>Terrain layer order. Array indices in painting code follow this order.</summary>
    public enum GroundLayer
    {
        GrassGround,
        GrassA,
        GrassDry,
        GrassPath,
        Gravel,
        AerialMud0,
        AerialMud90,
        DryMud,
    }

    public static readonly int GroundLayerCount = Enum.GetValues(typeof(GroundLayer)).Length;

    [MenuItem("Farm Land/Step 2 - Ground Materials")]
    public static void BuildGroundMaterials()
    {
        RequireEditMode();

        ConfigureSourceTextures();
        GenerateGroundTextures();
        BuildTerrain();
        PaintGroundTestPatches();
        AssetDatabase.SaveAssets();
    }

    /// <summary>Sets color space and texture type on source textures. Only changed files reimport.</summary>
    public static void ConfigureSourceTextures()
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string set in SourceTextureSets)
            {
                ConfigureTexture(SourceTexture(set, "diff"), TextureImporterType.Default, true);
                ConfigureTexture(SourceTexture(set, "nor_gl"), TextureImporterType.NormalMap, false);
                ConfigureTexture(SourceTexture(set, "rough"), TextureImporterType.Default, false);
                ConfigureTexture(SourceTexture(set, "disp"), TextureImporterType.Default, false);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    /// <summary>Creates mask maps for every source set and 90 degree copies of the aerial mud maps.</summary>
    public static void GenerateGroundTextures()
    {
        EnsureFolder(TerrainFolder);
        EnsureFolder(GeneratedFolder);
        EnsureFolder(LayersFolder);

        foreach (string set in SourceTextureSets)
            WriteMask(set, $"{set}_mask.png", rotate: false);

        WriteMask(AerialMud, $"{AerialMud}_mask_90.png", rotate: true);
        WriteFlattenedDiffuses();
        WriteRotatedDiffuse(AerialMud);
        WriteRotatedNormal(AerialMud);
    }

    /// <summary>
    /// Removes large bright and dark blotches from the track, field and dry grass textures. Those
    /// blotches are what reads as a repeating grid, while the fine detail doesn't.
    /// </summary>
    public static void WriteFlattenedDiffuses()
    {
        WriteFlattenedDiffuse(Gravel);
        WriteFlattenedDiffuse(GrassPath);
        WriteFlattenedDiffuse(AerialMud);
        WriteFlattenedSampleDiffuse("Grass_Dry_TerrainLayer", "GrassDry");
    }

    /// <summary>Creates or updates the terrain layers, terrain data, material and scene object.</summary>
    public static void BuildTerrain()
    {
        RequireEditMode();

        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        if (data == null)
        {
            data = new TerrainData
            {
                heightmapResolution = HeightmapResolution,
                alphamapResolution = AlphamapResolution,
                baseMapResolution = 1024,
                size = new Vector3(MapSize, MapMaxHeight, MapSize),
            };
            AssetDatabase.CreateAsset(data, TerrainDataPath);
        }
        data.terrainLayers = BuildTerrainLayers();

        Scene scene = OpenFarmLandScene();

        GameObject terrainObject = GameObject.Find(TerrainObjectName);
        if (terrainObject == null)
        {
            terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = TerrainObjectName;
        }

        Terrain terrain = terrainObject.GetComponent<Terrain>();
        terrain.terrainData = data;
        terrain.materialTemplate = LoadOrCreateTerrainMaterial();
        terrain.drawInstanced = true;
        terrain.basemapDistance = 1500f;
        terrain.heightmapPixelError = 5f;

        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>
    /// Paints test squares on a blended meadow background: one per grass layer and dry mud, and four
    /// of field surface. Gravel and path aren't shown here. Replaced by the real layout in Step 3.
    /// </summary>
    public static void PaintGroundTestPatches()
    {
        RequireEditMode();

        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        int resolution = data.alphamapResolution;
        int layers = data.terrainLayers.Length;
        var alpha = new float[resolution, resolution, layers];
        float texelSize = (float)MapSize / resolution;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
                PaintMeadow(alpha, z, x, x * texelSize, z * texelSize);
        }

        foreach (GroundLayer layer in Enum.GetValues(typeof(GroundLayer)))
        {
            Rect patch = TestPatchRect(layer);
            int minX = WorldToAlpha(patch.xMin, resolution);
            int maxX = WorldToAlpha(patch.xMax, resolution);
            int minZ = WorldToAlpha(patch.yMin, resolution);
            int maxZ = WorldToAlpha(patch.yMax, resolution);
            GroundLayer surface = TestPatchSurface(layer);
            bool isField = surface == GroundLayer.AerialMud0 || surface == GroundLayer.AerialMud90;
            for (int z = minZ; z < maxZ; z++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    for (int i = 0; i < layers; i++)
                        alpha[z, x, i] = 0f;

                    if (isField)
                        PaintField(alpha, z, x, surface, x * texelSize, z * texelSize);
                    else
                        alpha[z, x, (int)surface] = 1f;
                }
            }
        }

        data.SetAlphamaps(0, 0, alpha);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }

    // Mostly green grass, with drifts and larger areas of dry grass, tan patches, and burnt areas where
    // nothing green is left and bare earth shows through.
    static void PaintMeadow(float[,,] alpha, int z, int x, float worldX, float worldZ)
    {
        float green = BlendNoise(worldX, worldZ, 100f);
        float dryness = MeadowDryness(worldX, worldZ);
        float burnt = BurntShare(worldX, worldZ);

        float grassA = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, green))) * (1f - 0.7f * dryness) * (1f - burnt);
        float grassDry = 1.2f * dryness;
        float grassGround = 0.35f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.7f, green))) * (1f - burnt);
        float bareEarth = burnt * 0.7f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.62f, BlendNoise(worldX, worldZ, 1500f)));

        float total = grassA + grassDry + grassGround + bareEarth;
        alpha[z, x, (int)GroundLayer.GrassA] = grassA / total;
        alpha[z, x, (int)GroundLayer.GrassDry] = grassDry / total;
        alpha[z, x, (int)GroundLayer.GrassGround] = grassGround / total;
        alpha[z, x, (int)GroundLayer.DryMud] = bareEarth / total;
    }

    // Small drifts everywhere, plus whole areas of dry meadow. Used for both ground paint and grass.
    static float MeadowDryness(float worldX, float worldZ)
    {
        float drift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.72f, BlendNoise(worldX, worldZ, 300f)));
        float region = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.57f, 0.67f, DryRegionNoise(worldX, worldZ)));
        return Mathf.Max(drift, region);
    }

    // The middle of the dry areas, burnt by the sun with no green left, so meadow runs from green through
    // dry to burnt. Used for both ground paint and grass.
    static float BurntShare(float worldX, float worldZ)
    {
        float uneven = (BlendNoise(worldX, worldZ, 1700f) - 0.5f) * 0.12f;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.63f, 0.69f, DryRegionNoise(worldX, worldZ) + uneven));
    }

    static float DryRegionNoise(float worldX, float worldZ)
    {
        const float scale = 1f / 240f;
        const float offset = 40f;
        return Mathf.PerlinNoise(worldX * scale + offset, worldZ * scale + offset);
    }

    // Tilled soil with lighter, drier drifts that break up the repeating rows.
    static void PaintField(float[,,] alpha, int z, int x, GroundLayer mudLayer, float worldX, float worldZ)
    {
        float dryMud = FieldDryMudShare(worldX, worldZ);
        alpha[z, x, (int)GroundLayer.DryMud] = dryMud;
        alpha[z, x, (int)mudLayer] = 1f - dryMud;
    }

    // Crossfades whatever is already painted into an abandoned field: soil with weeds over it. Soil dries
    // out as a field overgrows, which also fades its furrows.
    static void BlendFieldSurface(float[,,] alpha, int z, int x, FarmField field, float coverage, float worldX, float worldZ, float signedDistance)
    {
        for (int i = 0; i < GroundLayerCount; i++)
            alpha[z, x, i] *= 1f - coverage;

        float weeds = field.WeedShare(worldX, worldZ, signedDistance);
        float soil = coverage * (1f - weeds);
        float dryMud = Mathf.Lerp(FieldDryMudShare(worldX, worldZ), 0.7f, field.Overgrowth * 0.6f);
        alpha[z, x, (int)GroundLayer.DryMud] += soil * dryMud;
        alpha[z, x, (int)field.MudLayer] += soil * (1f - dryMud);

        float weedCover = coverage * weeds;
        float burnt = BurntShare(worldX, worldZ);
        float green = 0.1f * (1f - burnt);
        float tan = 0.25f * (1f - burnt);

        // Soil showing through keeps an overgrown field's shape readable against dry meadow.
        float earth = 0.15f + 0.1f * burnt;
        alpha[z, x, (int)GroundLayer.GrassA] += weedCover * green;
        alpha[z, x, (int)GroundLayer.GrassGround] += weedCover * tan;
        alpha[z, x, (int)GroundLayer.DryMud] += weedCover * earth;
        alpha[z, x, (int)GroundLayer.GrassDry] += weedCover * (1f - green - tan - earth);
    }

    static float FieldDryMudShare(float worldX, float worldZ)
    {
        return 0.45f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.75f, BlendNoise(worldX, worldZ, 500f)));
    }

    // Road materials are for narrow tracks (Step 4), so their test squares show field surface instead.
    static GroundLayer TestPatchSurface(GroundLayer layer)
    {
        switch (layer)
        {
            case GroundLayer.GrassPath: return GroundLayer.AerialMud0;
            case GroundLayer.Gravel: return GroundLayer.AerialMud90;
            default: return layer;
        }
    }

    // Two octaves of Perlin noise in the range 0 to 1. The offset picks an independent pattern.
    static float BlendNoise(float worldX, float worldZ, float offset)
    {
        const float largeScale = 1f / 90f;
        const float smallScale = 1f / 15f;
        float large = Mathf.PerlinNoise(worldX * largeScale + offset, worldZ * largeScale + offset);
        float small = Mathf.PerlinNoise(worldX * smallScale + offset, worldZ * smallScale + offset);
        return large * 0.75f + small * 0.25f;
    }

    /// <summary>World-space XZ rectangle of a layer's test patch: 4 columns by 2 rows, 150 m wide.</summary>
    public static Rect TestPatchRect(GroundLayer layer)
    {
        const float patchSize = 150f;
        const float gap = 20f;
        const float originX = 170f;
        const float originZ = 300f;

        int index = (int)layer;
        int column = index % 4;
        int row = index / 4;
        return new Rect(originX + column * (patchSize + gap), originZ + row * (patchSize + gap), patchSize, patchSize);
    }

    static int WorldToAlpha(float world, int resolution)
    {
        return Mathf.Clamp(Mathf.RoundToInt(world / MapSize * resolution), 0, resolution);
    }

    static TerrainLayer[] BuildTerrainLayers()
    {
        var layers = new TerrainLayer[GroundLayerCount];

        // Tile sizes share no common multiple, so blended layers don't repeat in step with each other.
        TerrainLayer grassGround = SourceLayer("GrassGround", GrassGround, 13f);
        grassGround.diffuseRemapMax = new Vector4(0.8f, 1.05f, 0.65f, 1f);
        layers[(int)GroundLayer.GrassGround] = grassGround;

        layers[(int)GroundLayer.GrassA] = SampleLayer("GrassA", "Grass_A_TerrainLayer", 7f);

        // Whole areas of dry ground show this layer's tiling, so it uses a flattened copy.
        TerrainLayer grassDry = SampleLayer("GrassDry", "Grass_Dry_TerrainLayer", 11f);
        grassDry.diffuseTexture = LoadRequired<Texture2D>($"{GeneratedFolder}/GrassDry_diff_flat.jpg");
        layers[(int)GroundLayer.GrassDry] = grassDry;

        layers[(int)GroundLayer.GrassPath] = FlattenedSourceLayer("GrassPath", GrassPath, 5f);

        // Darker and browner, so main tracks read as muddy rather than clean gravel.
        TerrainLayer gravel = FlattenedSourceLayer("Gravel", Gravel, 6f);
        gravel.diffuseRemapMax = new Vector4(0.72f, 0.6f, 0.47f, 1f);
        layers[(int)GroundLayer.Gravel] = gravel;

        layers[(int)GroundLayer.DryMud] = SourceLayer("DryMud", DryMud, 17f);

        // The source is dark and slightly purple, so red is lifted most to read as brown soil.
        Vector4 mudBrighten = new Vector4(1.6f, 1.35f, 1.1f, 1f);
        TerrainLayer mud0 = FlattenedSourceLayer("AerialMud_0", AerialMud, MudTileSize);
        mud0.diffuseRemapMax = mudBrighten;
        layers[(int)GroundLayer.AerialMud0] = mud0;

        TerrainLayer mud90 = LoadOrCreateLayer("AerialMud_90");
        mud90.diffuseTexture = LoadRequired<Texture2D>($"{GeneratedFolder}/{AerialMud}_diff_90.jpg");
        mud90.normalMapTexture = LoadRequired<Texture2D>($"{GeneratedFolder}/{AerialMud}_nor_gl_90.png");
        mud90.maskMapTexture = LoadRequired<Texture2D>($"{GeneratedFolder}/{AerialMud}_mask_90.png");
        mud90.tileSize = new Vector2(MudTileSize, MudTileSize);
        mud90.diffuseRemapMax = mudBrighten;
        EditorUtility.SetDirty(mud90);
        layers[(int)GroundLayer.AerialMud90] = mud90;

        foreach (TerrainLayer layer in layers)
            EditorUtility.SetDirty(layer);

        return layers;
    }

    static TerrainLayer SourceLayer(string name, string set, float tileSize)
    {
        TerrainLayer layer = LoadOrCreateLayer(name);
        layer.diffuseTexture = LoadRequired<Texture2D>(SourceTexture(set, "diff"));
        layer.normalMapTexture = LoadRequired<Texture2D>(SourceTexture(set, "nor_gl"));
        layer.maskMapTexture = LoadRequired<Texture2D>($"{GeneratedFolder}/{set}_mask.png");
        layer.tileSize = new Vector2(tileSize, tileSize);
        return layer;
    }

    static TerrainLayer FlattenedSourceLayer(string name, string set, float tileSize)
    {
        TerrainLayer layer = SourceLayer(name, set, tileSize);
        layer.diffuseTexture = LoadRequired<Texture2D>($"{GeneratedFolder}/{set}_diff_flat.jpg");
        return layer;
    }

    // Copies a sample layer so its tile size can change without editing TerrainSampleAssets.
    static TerrainLayer SampleLayer(string name, string sampleLayerName, float tileSize)
    {
        string path = $"{LayersFolder}/{name}.terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            TerrainLayer sample = LoadRequired<TerrainLayer>($"{SampleLayersFolder}/{sampleLayerName}.terrainlayer");
            layer = UnityEngine.Object.Instantiate(sample);
            AssetDatabase.CreateAsset(layer, path);
        }
        layer.tileSize = new Vector2(tileSize, tileSize);
        return layer;
    }

    static TerrainLayer LoadOrCreateLayer(string name)
    {
        string path = $"{LayersFolder}/{name}.terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }
        return layer;
    }

    static Material LoadOrCreateTerrainMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { enableInstancing = true };
            AssetDatabase.CreateAsset(material, TerrainMaterialPath);
        }
        return material;
    }

    static Scene OpenFarmLandScene()
    {
        Scene active = SceneManager.GetActiveScene();
        return active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    // Terrain mask layout: R metallic, G occlusion, B height, A smoothness.
    static void WriteMask(string set, string fileName, bool rotate)
    {
        string path = $"{GeneratedFolder}/{fileName}";
        int size = GeneratedTextureSize;
        Color[] rough = ReadLinearPixels(LoadRequired<Texture2D>(SourceTexture(set, "rough")), size);
        Color[] height = ReadLinearPixels(LoadRequired<Texture2D>(SourceTexture(set, "disp")), size);

        var mask = new Color32[size * size];
        for (int i = 0; i < mask.Length; i++)
        {
            byte h = (byte)Mathf.RoundToInt(Mathf.Clamp01(height[i].r) * 255f);
            byte smoothness = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - rough[i].r) * 255f);
            mask[i] = new Color32(0, 255, h, smoothness);
        }

        if (rotate)
            mask = RotateCounterClockwise(mask, size);

        WritePng(path, mask, size);
        ImportGenerated(path, TextureImporterType.Default, false);
    }

    // Rotates the flattened copy, so both furrow directions look the same.
    static void WriteRotatedDiffuse(string set)
    {
        string path = $"{GeneratedFolder}/{set}_diff_90.jpg";
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        source.LoadImage(File.ReadAllBytes($"{GeneratedFolder}/{set}_diff_flat.jpg"));
        int size = source.width;
        Color32[] rotated = RotateCounterClockwise(source.GetPixels32(), size);
        UnityEngine.Object.DestroyImmediate(source);

        var output = new Texture2D(size, size, TextureFormat.RGB24, false);
        output.SetPixels32(rotated);
        File.WriteAllBytes(path, output.EncodeToJPG(95));
        UnityEngine.Object.DestroyImmediate(output);
        ImportGenerated(path, TextureImporterType.Default, true);
    }

    static void WriteFlattenedDiffuse(string set)
    {
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        source.LoadImage(File.ReadAllBytes(SourceTexture(set, "diff")));
        int size = source.width;
        Color32[] pixels = source.GetPixels32();
        UnityEngine.Object.DestroyImmediate(source);

        WriteFlattenedPixels($"{GeneratedFolder}/{set}_diff_flat.jpg", pixels, size);
    }

    // Sample textures are TIFs, which LoadImage can't decode, so this reads the imported texture instead.
    static void WriteFlattenedSampleDiffuse(string sampleLayerName, string name)
    {
        Texture2D source = LoadRequired<TerrainLayer>($"{SampleLayersFolder}/{sampleLayerName}.terrainlayer").diffuseTexture;
        int size = source.width;
        WriteFlattenedPixels($"{GeneratedFolder}/{name}_diff_flat.jpg", ReadSrgbPixels(source, size), size);
    }

    static Color32[] ReadSrgbPixels(Texture source, int size)
    {
        RenderTexture target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, target);
        RenderTexture.active = target;

        var readback = new Texture2D(size, size, TextureFormat.RGBA32, false);
        readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        readback.Apply();
        Color32[] pixels = readback.GetPixels32();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        UnityEngine.Object.DestroyImmediate(readback);
        return pixels;
    }

    static void WriteFlattenedPixels(string path, Color32[] pixels, int size)
    {
        const int lowResolution = 256;
        const int blurRadius = 16;
        const int blurPasses = 3;

        int blockSize = size / lowResolution;
        var low = new float[3][];
        var mean = new float[3];
        for (int channel = 0; channel < 3; channel++)
            low[channel] = new float[lowResolution * lowResolution];

        for (int y = 0; y < lowResolution * blockSize; y++)
        {
            for (int x = 0; x < lowResolution * blockSize; x++)
            {
                Color32 pixel = pixels[y * size + x];
                int lowIndex = y / blockSize * lowResolution + x / blockSize;
                low[0][lowIndex] += pixel.r;
                low[1][lowIndex] += pixel.g;
                low[2][lowIndex] += pixel.b;
            }
        }

        float samplesPerBlock = blockSize * blockSize;
        for (int channel = 0; channel < 3; channel++)
        {
            float total = 0f;
            for (int i = 0; i < low[channel].Length; i++)
            {
                low[channel][i] /= samplesPerBlock;
                total += low[channel][i];
            }
            mean[channel] = total / low[channel].Length;

            for (int pass = 0; pass < blurPasses; pass++)
                BlurWrapped(low[channel], lowResolution, blurRadius);
        }

        var flattened = new Color32[pixels.Length];
        float scale = (float)lowResolution / size;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color32 pixel = pixels[y * size + x];
                flattened[y * size + x] = new Color32(
                    Flatten(pixel.r, mean[0], SampleWrapped(low[0], lowResolution, x * scale, y * scale)),
                    Flatten(pixel.g, mean[1], SampleWrapped(low[1], lowResolution, x * scale, y * scale)),
                    Flatten(pixel.b, mean[2], SampleWrapped(low[2], lowResolution, x * scale, y * scale)),
                    255);
            }
        }

        var output = new Texture2D(size, size, TextureFormat.RGB24, false);
        output.SetPixels32(flattened);
        File.WriteAllBytes(path, output.EncodeToJPG(95));
        UnityEngine.Object.DestroyImmediate(output);
        ImportGenerated(path, TextureImporterType.Default, true);
    }

    static byte Flatten(byte value, float mean, float localMean)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(value * mean / Mathf.Max(localMean, 1f)), 0, 255);
    }

    // Box blur in both axes. Wrapping at the edges keeps the texture seamless.
    static void BlurWrapped(float[] values, int size, int radius)
    {
        var buffer = new float[values.Length];
        float weight = 1f / (radius * 2 + 1);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float sum = 0f;
                for (int offset = -radius; offset <= radius; offset++)
                    sum += values[y * size + (x + offset + size) % size];
                buffer[y * size + x] = sum * weight;
            }
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float sum = 0f;
                for (int offset = -radius; offset <= radius; offset++)
                    sum += buffer[(y + offset + size) % size * size + x];
                values[y * size + x] = sum * weight;
            }
        }
    }

    static float SampleWrapped(float[] values, int size, float x, float y)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float fx = x - x0;
        float fy = y - y0;
        int x1 = (x0 + 1) % size;
        int y1 = (y0 + 1) % size;
        x0 %= size;
        y0 %= size;

        float bottom = Mathf.Lerp(values[y0 * size + x0], values[y0 * size + x1], fx);
        float top = Mathf.Lerp(values[y1 * size + x0], values[y1 * size + x1], fx);
        return Mathf.Lerp(bottom, top, fy);
    }

    // A rotated image also needs its tangent-space vectors rotated, so (x, y) becomes (-y, x).
    static void WriteRotatedNormal(string set)
    {
        string path = $"{GeneratedFolder}/{set}_nor_gl_90.png";
        int size = GeneratedTextureSize;
        Color[] packed = ReadLinearPixels(LoadRequired<Texture2D>(SourceTexture(set, "nor_gl")), size);

        var source = new Color32[size * size];
        for (int i = 0; i < source.Length; i++)
        {
            // Works for both DXT5nm (x in alpha, red is 1) and BC5 (x in red, alpha is 1).
            float x = packed[i].a * packed[i].r * 2f - 1f;
            float y = packed[i].g * 2f - 1f;
            float z = Mathf.Sqrt(Mathf.Clamp01(1f - x * x - y * y));
            source[i] = new Color32(ToUnorm(-y), ToUnorm(x), ToUnorm(z), 255);
        }

        WritePng(path, RotateCounterClockwise(source, size), size);
        ImportGenerated(path, TextureImporterType.NormalMap, false);
    }

    static byte ToUnorm(float value)
    {
        return (byte)Mathf.RoundToInt(Mathf.Clamp01(value * 0.5f + 0.5f) * 255f);
    }

    // Pixels are stored with y = 0 at the bottom, so this is a counter-clockwise turn in UV space.
    static Color32[] RotateCounterClockwise(Color32[] source, int size)
    {
        var rotated = new Color32[source.Length];
        for (int sourceY = 0; sourceY < size; sourceY++)
        {
            for (int sourceX = 0; sourceX < size; sourceX++)
            {
                int targetX = size - 1 - sourceY;
                int targetY = sourceX;
                rotated[targetY * size + targetX] = source[sourceY * size + sourceX];
            }
        }
        return rotated;
    }

    static Color[] ReadLinearPixels(Texture source, int size)
    {
        RenderTexture target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, target);
        RenderTexture.active = target;

        var readback = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
        readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        readback.Apply();
        Color[] pixels = readback.GetPixels();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        UnityEngine.Object.DestroyImmediate(readback);
        return pixels;
    }

    static void WritePng(string path, Color32[] pixels, int size)
    {
        byte[] png = ImageConversion.EncodeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)size, (uint)size);
        File.WriteAllBytes(path, png);
    }

    static void ImportGenerated(string path, TextureImporterType type, bool sRGB)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        ConfigureTexture(path, type, sRGB);
    }

    static void ConfigureTexture(string path, TextureImporterType type, bool sRGB)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
            throw new FileNotFoundException("FarmLandBuilder: not an imported texture", path);

        bool changed = importer.textureType != type
            || importer.sRGBTexture != sRGB
            || importer.anisoLevel != GroundAnisoLevel
            || importer.wrapMode != TextureWrapMode.Repeat;
        if (!changed)
            return;

        importer.textureType = type;
        importer.sRGBTexture = sRGB;
        importer.anisoLevel = GroundAnisoLevel;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.alphaIsTransparency = false;
        importer.SaveAndReimport();
    }

    static string SourceTexture(string set, string kind)
    {
        string folder = $"{SourceTexturesRoot}/{set}_4k/textures";
        string prefix = $"{set}_{kind}_4k.";
        foreach (string file in Directory.GetFiles(folder))
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith(prefix, StringComparison.Ordinal) && !name.EndsWith(".meta", StringComparison.Ordinal))
                return $"{folder}/{name}";
        }
        throw new FileNotFoundException($"FarmLandBuilder: no {kind} texture for {set}", folder);
    }

    // Scene and asset edits made in Play mode would only partly apply.
    static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("FarmLandBuilder: exit Play mode before running a build step.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path), Path.GetFileName(path));
    }
}
