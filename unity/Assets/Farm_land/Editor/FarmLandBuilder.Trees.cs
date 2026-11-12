using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Step 6: tree prefabs. Pines reuse the Assets/Art meshes and textures with rebuilt URP materials. The
/// island tree comes from Models/build_island_tree.py, which runs in Blender rather than here.
/// </summary>
public static partial class FarmLandBuilder
{
    const string TreesFolder = "Assets/Farm_land/Prefabs/Trees";
    const string ModelsFolder = "Assets/Farm_land/Models";
    const string PinesRoot = "Assets/Art/Environment/Vegetation/Trees/Pines";
    const string IslandTexturesFolder = "Assets/Farm_land/Materials/island_tree_01_4k/textures";
    const string IslandModelPath = ModelsFolder + "/island_tree_reduced.fbx";
    const string IslandLeavesAtlasPath = ModelsFolder + "/island_tree_leaves.png";
    const string IslandImpostorAtlasPath = ModelsFolder + "/island_tree_impostor.png";
    const string IslandDryLeavesAtlasPath = ModelsFolder + "/island_tree_dry_leaves.png";
    const string IslandDryImpostorAtlasPath = ModelsFolder + "/island_tree_dry_impostor.png";
    const string IslandPrefabName = "IslandTree";
    const string IslandDryPrefabName = "IslandTree_Dry";
    const string LeavesShaderName = "Farm Land/Tree Leaves";
    const string BarkShaderName = "Farm Land/Tree Bark";
    const string LitShaderName = "Universal Render Pipeline/Lit";
    const float TreeCutoff = 0.5f;

    // Tree wind, shared by bark and foliage so every part of a tree moves together. Sway is how far the
    // crown moves at 10 m height, and grows with the square of height. Flutter
    // moves leaves and twigs away from the trunk. Impostor cards only sway, since flutter would shake whole
    // cards. Gusts match the defaults in FarmLandGrass.shader, so a gust bends grass and trees together.
    const float TreeSway = 0.15f;
    const float TreeFlutter = 0.08f;
    const float WindSpeed = 6f;
    const float WindGustSize = 35f;
    static readonly Vector4 WindDirection = new Vector4(0.8f, 0f, 0.6f, 0f);

    // The impostor gets no self-shadowing, so it's darker to match the LODs before it.
    static readonly Color IslandImpostorTint = new Color(0.82f, 0.82f, 0.82f);
    static readonly Color IslandDryImpostorTint = new Color(0.74f, 0.76f, 0.8f);
    const float ImpostorEdgeFade = 0.3f;

    // Four crossed views cover more of the screen than the tree does, so the impostor clips harder.
    const float ImpostorCutoff = 0.65f;

    // The dry crown has gaps that the four views fill in, so it clips harder still.
    const float DryImpostorCutoff = 0.8f;

    // HDRP lit the pine foliage mostly by transmission, so its textures are far too dark on their own, at
    // about 0.07 linear. Values above 1 brighten, and these also pull each family towards the same green.
    static readonly Dictionary<string, Color> PineFoliageTints = new Dictionary<string, Color>
    {
        ["Pine002"] = new Color(1.1f, 1.3f, 1.15f),
        ["Pine004"] = new Color(1.25f, 1.3f, 1.35f),
        ["Pine006"] = new Color(1.25f, 1.3f, 1.35f),
    };
    const float PineNormalUp = 0.4f;

    // Dry needles turn a dull brown. The textures are dark and nearly neutral, so a warm tint does most of the
    // work, and more red than this reads as autumn orange.
    static readonly Dictionary<string, Color> PineDryFoliageTints = new Dictionary<string, Color>
    {
        ["Pine002"] = new Color(1.42f, 1.22f, 1.12f),
        ["Pine004"] = new Color(1.45f, 1.25f, 1.3f),
        ["Pine006"] = new Color(1.45f, 1.25f, 1.3f),

        // Bare wood, whose texture is already at its real brightness.
        ["PineDead"] = Color.white,
    };

    // Pine_004 asks for a normal scale of 20, which reads as noise here.
    const float TreeMaxNormalScale = 2f;

    const float TreeMaxSmoothness = 0.2f;

    const int LeavesAtlasMaxSize = 2048;
    const int ImpostorAtlasMaxSize = 1024;
    const int TreeAnisoLevel = 4;

    // Island tree LOD switches as a share of screen height. Each LOD's value is where it stops being used.
    static readonly float[] IslandLodHeights = { 0.25f, 0.08f, 0.005f };

    // Keeps even the smallest pine, 6.5 m tall, drawn past the map's 1.4 km diagonal.
    const float PineCullHeight = 0.003f;

    // Each row is a source prefab under PinesRoot and the name of its copy. These all end in an impostor,
    // except Pine006, which is cheap at every distance. Pine_002_XL is left out, since it costs 54k triangles
    // and Pine_002_L is as tall for 12k. Each family shares one set of materials, apart from billboards, whose
    // brightness is set per prefab to match its last 3D LOD at the distance they switch. PineDead has no
    // impostor and stays at 11k triangles when far away, so keep it to a few accents. PineDead_03 is left out
    // because it's nearly the same mesh as PineDead_02.
    static readonly (string family, string folder, string source, string name, float billboardBrightness, bool dry)[] PinePrefabs =
    {
        ("Pine002", "Pine_002_new", "Pine_002_M", "Pine002_M", 1.45f, false),
        ("Pine002", "Pine_002_new", "Pine_002_S2", "Pine002_S2", 1f, false),
        ("Pine002", "Pine_002_new", "Pine_002_L", "Pine002_L", 1f, false),
        ("Pine004", "Pine_004_new", "pine_004_1", "Pine004_1", 1.4f, false),
        ("Pine004", "Pine_004_new", "pine_004_3", "Pine004_3", 1.15f, false),
        ("Pine006", "Pine_006", "pine_006_01", "Pine006_01", 1f, false),
        ("Pine006", "Pine_006", "pine_006_02", "Pine006_02", 1f, false),
        ("Pine002", "Pine_002_new", "Pine_002_M", "Pine002_M_Dry", 1.45f, true),
        ("Pine004", "Pine_004_new", "pine_004_1", "Pine004_1_Dry", 1.5f, true),
        ("Pine006", "Pine_006", "pine_006_01", "Pine006_01_Dry", 1f, true),
        ("PineDead", "PineDead_001", "PineDead_02", "PineDead_02", 1f, true),
    };

    [MenuItem("Farm Land/Step 6 - Tree Assets")]
    public static void BuildTreeAssets()
    {
        RequireEditMode();
        EnsureFolder("Assets/Farm_land/Prefabs");
        EnsureFolder(TreesFolder);
        EnsureFolder(UrpMaterialsFolder);

        PineMaterialsBuilt.Clear();
        foreach (var pine in PinePrefabs)
            BuildPinePrefab(pine.family, pine.folder, pine.source, pine.name, pine.billboardBrightness, pine.dry);
        BuildIslandTreePrefabs();
        AssetDatabase.SaveAssets();
    }

    /// <summary>Copies a pine prefab with rebuilt materials, and leaves the original untouched.</summary>
    static void BuildPinePrefab(string family, string folder, string source, string name, float billboardBrightness, bool dry)
    {
        GameObject sourcePrefab = LoadRequired<GameObject>($"{PinesRoot}/{folder}/{source}.prefab");
        GameObject copy = Object.Instantiate(sourcePrefab);
        copy.name = name;

        // The landscape is viewed with a free-fly camera, so nothing collides with trees.
        foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);

        foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = BuildPineMaterial(family, name, billboardBrightness, dry, materials[i]);
            renderer.sharedMaterials = materials;
        }

        LODGroup group = copy.GetComponent<LODGroup>();
        MergeIdenticalLods(group);
        ExtendLastLod(group);
        PrefabUtility.SaveAsPrefabAsset(copy, $"{TreesFolder}/{name}.prefab");
        Object.DestroyImmediate(copy);
    }

    // The source prefabs cull as close as 290 m, so whole groups would vanish across the map.
    static void ExtendLastLod(LODGroup group)
    {
        LOD[] lods = group.GetLODs();
        LOD last = lods[lods.Length - 1];
        last.screenRelativeTransitionHeight = Mathf.Min(last.screenRelativeTransitionHeight, PineCullHeight);
        lods[lods.Length - 1] = last;
        group.SetLODs(lods);
    }

    // The source LODs 0 and 1 often share a mesh, differing only in a displacement this project doesn't
    // have. Drawing the same mesh under two LODs does nothing, so the first takes over the second's range.
    static void MergeIdenticalLods(LODGroup group)
    {
        var merged = new List<LOD>();
        foreach (LOD lod in group.GetLODs())
        {
            if (merged.Count > 0 && SameMesh(merged[merged.Count - 1], lod))
            {
                LOD previous = merged[merged.Count - 1];
                previous.screenRelativeTransitionHeight = lod.screenRelativeTransitionHeight;
                merged[merged.Count - 1] = previous;
                foreach (Renderer renderer in lod.renderers)
                    Object.DestroyImmediate(renderer.gameObject);
                continue;
            }
            merged.Add(lod);
        }
        group.SetLODs(merged.ToArray());
    }

    static bool SameMesh(LOD a, LOD b)
    {
        return a.renderers.Length == 1 && b.renderers.Length == 1
            && a.renderers[0].GetComponent<MeshFilter>().sharedMesh == b.renderers[0].GetComponent<MeshFilter>().sharedMesh;
    }

    // The source materials have a missing shader, so their values only exist as saved properties.
    static Material BuildPineMaterial(string family, string prefabName, float billboardBrightness, bool dry, Material source)
    {
        string role = PineMaterialRole(source.name);
        bool billboard = role == "Billboards";
        if (dry && role == "Branches")
            role = "DryBranches";
        string path = $"{UrpMaterialsFolder}/{(billboard ? prefabName : family)}_{role}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null && PineMaterialsBuilt.Contains(path))
            return existing;
        PineMaterialsBuilt.Add(path);

        Texture baseMap = SavedTexture(source, "_BaseColorMap");
        if (SavedFloat(source, "_AlphaCutoffEnable", 0f) > 0.5f)
        {
            Color tint = (dry ? PineDryFoliageTints : PineFoliageTints)[family] * (billboard ? billboardBrightness : 1f);
            tint.a = 1f;
            return BuildFoliageMaterial(path, baseMap, tint, SavedFloat(source, "_AlphaCutoff", TreeCutoff), 0f, PineNormalUp, 1f, 0f, billboard ? 0f : TreeFlutter);
        }

        return BuildBarkMaterial(
            path,
            baseMap,
            SavedTexture(source, "_NormalMap"),
            SavedTexture(source, "_MaskMap"),
            Mathf.Min(SavedFloat(source, "_NormalScale", 1f), TreeMaxNormalScale),
            Mathf.Min(SavedFloat(source, "_SmoothnessRemapMax", 0f) * 0.5f, TreeMaxSmoothness));
    }

    static readonly HashSet<string> PineMaterialsBuilt = new HashSet<string>();

    static string PineMaterialRole(string sourceName)
    {
        string lower = sourceName.ToLowerInvariant();
        if (lower.Contains("branches"))
            return "Branches";
        if (lower.Contains("billboards"))
            return "Billboards";
        if (lower.Contains("stump"))
            return "Stump";
        return "Trunk";
    }

    static Material BuildWoodMaterial(string path, Texture baseMap, Texture normalMap, Texture occlusionMap, float normalScale, float smoothness)
    {
        return BuildWoodMaterial(path, LitShaderName, baseMap, normalMap, occlusionMap, normalScale, smoothness);
    }

    // Tree wood uses the same surface as URP Lit, in a shader that sways with the foliage.
    static Material BuildBarkMaterial(string path, Texture baseMap, Texture normalMap, Texture occlusionMap, float normalScale, float smoothness)
    {
        Material material = BuildWoodMaterial(path, BarkShaderName, baseMap, normalMap, occlusionMap, normalScale, smoothness);
        SetTreeWind(material, TreeFlutter);
        return material;
    }

    static Material BuildWoodMaterial(string path, string shaderName, Texture baseMap, Texture normalMap, Texture occlusionMap, float normalScale, float smoothness)
    {
        Material material = LoadOrCreateMaterial(path, Shader.Find(shaderName));

        material.SetTexture("_BaseMap", baseMap);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_WorkflowMode", 1f);

        material.SetTexture("_BumpMap", normalMap);
        material.SetFloat("_BumpScale", normalScale);
        SetKeyword(material, "_NORMALMAP", normalMap != null);

        material.SetTexture("_OcclusionMap", occlusionMap);
        SetKeyword(material, "_OCCLUSIONMAP", occlusionMap != null);

        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material BuildFoliageMaterial(string path, Texture baseMap, Color tint, float cutoff, float vertexColorShare, float normalUp, float receiveShadows, float edgeFade, float flutter)
    {
        Material material = LoadOrCreateMaterial(path, Shader.Find(LeavesShaderName));
        material.SetTexture("_BaseMap", baseMap);
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Cutoff", cutoff);
        material.SetFloat("_VertexColorShare", vertexColorShare);
        material.SetFloat("_NormalUp", normalUp);
        material.SetFloat("_ReceiveShadows", receiveShadows);
        material.SetFloat("_EdgeFade", edgeFade);
        SetTreeWind(material, flutter);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void SetTreeWind(Material material, float flutter)
    {
        material.SetFloat("_TreeSway", TreeSway);
        material.SetFloat("_TreeFlutter", flutter);
        material.SetFloat("_WindSpeed", WindSpeed);
        material.SetFloat("_WindGustSize", WindGustSize);
        material.SetVector("_WindDirection", WindDirection);
    }

    static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);
    }

    static Material LoadOrCreateMaterial(string path, Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        return material;
    }

    static Texture SavedTexture(Material source, string name)
    {
        SerializedProperty textures = new SerializedObject(source).FindProperty("m_SavedProperties.m_TexEnvs");
        for (int i = 0; i < textures.arraySize; i++)
        {
            SerializedProperty entry = textures.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == name)
                return entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
        }
        return null;
    }

    static float SavedFloat(Material source, string name, float fallback)
    {
        SerializedProperty floats = new SerializedObject(source).FindProperty("m_SavedProperties.m_Floats");
        for (int i = 0; i < floats.arraySize; i++)
        {
            SerializedProperty entry = floats.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == name)
                return entry.FindPropertyRelative("second").floatValue;
        }
        return fallback;
    }

    static void BuildIslandTreePrefabs()
    {
        ConfigureIslandAssets();

        Material bark = BuildIslandWoodMaterial("IslandTree_Bark", "island_tree_01_diff_4k.jpg", "island_tree_01_nor_gl_4k.exr");
        Material twigs = BuildIslandWoodMaterial("IslandTree_Twigs", "island_tree_01_branches_diff_4k.png", "island_tree_01_branches_nor_gl_4k.png");

        BuildIslandTreePrefab(IslandPrefabName, "island_tree", bark, twigs,
            IslandLeavesMaterial("IslandTree_Leaves", IslandLeavesAtlasPath),
            IslandImpostorMaterial("IslandTree_Impostor", IslandImpostorAtlasPath, IslandImpostorTint, ImpostorCutoff));
        BuildIslandTreePrefab(IslandDryPrefabName, "island_tree_dry", bark, twigs,
            IslandLeavesMaterial("IslandTree_DryLeaves", IslandDryLeavesAtlasPath),
            IslandImpostorMaterial("IslandTree_DryImpostor", IslandDryImpostorAtlasPath, IslandDryImpostorTint, DryImpostorCutoff));
    }

    static Material IslandLeavesMaterial(string name, string atlasPath)
    {
        return BuildFoliageMaterial($"{UrpMaterialsFolder}/{name}.mat", LoadRequired<Texture>(atlasPath), Color.white, TreeCutoff, 1f, 0f, 1f, 0f, TreeFlutter);
    }

    static Material IslandImpostorMaterial(string name, string atlasPath, Color tint, float cutoff)
    {
        return BuildFoliageMaterial($"{UrpMaterialsFolder}/{name}.mat", LoadRequired<Texture>(atlasPath), tint, cutoff, 1f, 0f, 0f, ImpostorEdgeFade, 0f);
    }

    static void BuildIslandTreePrefab(string prefabName, string meshPrefix, Material bark, Material twigs, Material leaves, Material impostor)
    {
        var materials = new Dictionary<string, Material>
        {
            ["IslandTree_Bark"] = bark,
            ["IslandTree_Twigs"] = twigs,
            ["IslandTree_Leaves"] = leaves,
        };

        GameObject model = LoadRequired<GameObject>(IslandModelPath);
        var root = new GameObject(prefabName);
        LODGroup group = root.AddComponent<LODGroup>();
        var lods = new LOD[IslandLodHeights.Length];

        for (int i = 0; i < lods.Length; i++)
        {
            string name = $"{meshPrefix}_LOD{i}";
            Transform part = model.transform.Find(name);
            if (part == null)
                throw new System.IO.FileNotFoundException($"FarmLandBuilder: {name} is missing from the model", IslandModelPath);

            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            child.transform.SetLocalPositionAndRotation(part.localPosition, part.localRotation);
            child.transform.localScale = part.localScale;
            child.AddComponent<MeshFilter>().sharedMesh = part.GetComponent<MeshFilter>().sharedMesh;

            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            bool last = i == lods.Length - 1;
            renderer.sharedMaterials = IslandSubmeshMaterials(part.GetComponent<MeshRenderer>(), materials, last ? impostor : null);
            lods[i] = new LOD(IslandLodHeights[i], new Renderer[] { renderer });
        }

        group.SetLODs(lods);
        group.RecalculateBounds();
        PrefabUtility.SaveAsPrefabAsset(root, $"{TreesFolder}/{prefabName}.prefab");
        Object.DestroyImmediate(root);
    }

    // The model's imported materials name each submesh, and hold nothing else that's used.
    static Material[] IslandSubmeshMaterials(MeshRenderer imported, Dictionary<string, Material> materials, Material impostor)
    {
        Material[] slots = imported.sharedMaterials;
        var result = new Material[slots.Length];
        for (int i = 0; i < slots.Length; i++)
            result[i] = impostor != null ? impostor : materials[slots[i].name];
        return result;
    }

    static void ConfigureIslandAssets()
    {
        AssetDatabase.ImportAsset(IslandModelPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(IslandLeavesAtlasPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(IslandImpostorAtlasPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(IslandDryLeavesAtlasPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(IslandDryImpostorAtlasPath, ImportAssetOptions.ForceSynchronousImport);

        var model = (ModelImporter)AssetImporter.GetAtPath(IslandModelPath);
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

        ConfigureCutoutAtlas(IslandLeavesAtlasPath, LeavesAtlasMaxSize);
        ConfigureCutoutAtlas(IslandImpostorAtlasPath, ImpostorAtlasMaxSize);
        ConfigureCutoutAtlas(IslandDryLeavesAtlasPath, LeavesAtlasMaxSize);
        ConfigureCutoutAtlas(IslandDryImpostorAtlasPath, ImpostorAtlasMaxSize);

        ConfigureTexture($"{IslandTexturesFolder}/island_tree_01_diff_4k.jpg", TextureImporterType.Default, true);
        ConfigureTexture($"{IslandTexturesFolder}/island_tree_01_nor_gl_4k.exr", TextureImporterType.NormalMap, false);
        ConfigureTexture($"{IslandTexturesFolder}/island_tree_01_branches_diff_4k.png", TextureImporterType.Default, true);
        ConfigureTexture($"{IslandTexturesFolder}/island_tree_01_branches_nor_gl_4k.png", TextureImporterType.NormalMap, false);
    }

    // Coverage-preserving mips stop cards from thinning out with distance, and treating alpha as transparency
    // spreads colour into the empty pixels so edges don't pick up the black background.
    static void ConfigureCutoutAtlas(string path, int maxSize)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.mipMapsPreserveCoverage = true;
        importer.alphaTestReferenceValue = TreeCutoff;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.anisoLevel = TreeAnisoLevel;
        importer.maxTextureSize = maxSize;
        importer.SaveAndReimport();
    }

    static Material BuildIslandWoodMaterial(string name, string baseFile, string normalFile)
    {
        return BuildBarkMaterial(
            $"{UrpMaterialsFolder}/{name}.mat",
            LoadRequired<Texture>($"{IslandTexturesFolder}/{baseFile}"),
            LoadRequired<Texture>($"{IslandTexturesFolder}/{normalFile}"),
            null,
            1f,
            0.1f);
    }
}
