using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Step 11: bush prefabs, green and dry. The large bushes reuse Quixel meshes and textures from Assets/Art
/// with rebuilt materials, and the small ones reuse the TerrainSampleAssets bushes. Leaves use the tree
/// foliage shader and wood uses the tree bark shader, so bushes move in the same wind as the trees. Dry
/// copies grey out the green leaves and warm them with a tint.
/// </summary>
public static partial class FarmLandBuilder
{
    const string BushesFolder = "Assets/Farm_land/Prefabs/Bushes";
    const string QuixelRoot = "Assets/Art/Environment/_ExternalContent/Quixel";
    const string SampleBushesFolder = "Assets/TerrainSampleAssets/Prefabs";
    // Step 11 reviewed the bushes in a lineup in the scene, which Step 7 removes.
    const string BushLineupObjectName = "Bush Lineup";

    // Bushes are a few metres tall, so this keeps the smallest drawn to about 300 m.
    const float BushCullHeight = 0.006f;

    // Normals bend up like the pines', so leaves facing away from the sun don't go dark.
    const float BushNormalUp = 0.4f;

    // Dry copies: most of the green goes, then a warm tint turns what's left to straw and brown.
    const float BushDryDesaturate = 0.85f;
    static readonly Color BushDryTint = new Color(1.45f, 1.15f, 0.8f);

    // Like the pines, the Quixel leaf textures were lit mostly by transmission in HDRP and are too dark on
    // their own, and their source tints darken them further. These replace the source tints, keyed by source
    // material, and bring each to about the island tree leaves' brightness.
    static readonly Dictionary<string, float> BushFoliageBrightness = new Dictionary<string, float>
    {
        ["GreenBush_mat"] = 1.2f,
        ["Juniper_Bush_01"] = 1.6f,
        ["RedBush"] = 1.1f,
        ["Bush_Twig_01"] = 1f,
        ["Bush_Dead_Br_01"] = 0.9f,
        ["Plant_Perennials_qilgP2"] = 1.35f,
        ["Bush_qilgY2"] = 1.35f,
    };

    // The sample bushes are under a metre tall.
    const float SampleBushScale = 1.6f;

    // Each row is a source prefab and the name of its copy, with whether it's a dried-out copy.
    static readonly (string source, string name, bool dry)[] BushPrefabs =
    {
        ("Forest-quixel/GreenBush/GreenBush_Var01_Prefab", "GreenBush", false),
        ("Forest-quixel/Juniper_Bush_01/Juniper_Bush_01_Var1", "Juniper_1", false),
        ("Forest-quixel/Juniper_Bush_01/Juniper_Bush_01_Var2", "Juniper_2", false),
        ("Forest-quixel/Juniper_Bush_01/Juniper_Bush_01_Var3", "Juniper_3", false),
        ("Forest-quixel/Juniper_Bush_01/Juniper_Bush_01_Var4", "Juniper_4", false),
        ("Forest-quixel/RedBush/RedBush_Var1_Prefab", "RedBush", false),
        ("Megascans/Plants/Bush_qilgP2/Bush_qilgP2_6x6x4_PF", "LeafyBush_P2", false),
        ("Megascans/Plants/Bush_qilgY2/Bush_qilgY2_2x2x4_PF", "LeafyBush_Y2", false),
        ("Forest-quixel/GreenBush/GreenBush_Var01_Prefab", "GreenBush_Dry", true),
        ("Forest-quixel/Juniper_Bush_01/Juniper_Bush_01_Var1", "Juniper_1_Dry", true),
        ("Forest-quixel/RedBush/RedBush_Var1_Prefab", "RedBush_Dry", true),
        ("Megascans/Plants/Bush_qilgP2/Bush_qilgP2_6x6x4_PF", "LeafyBush_P2_Dry", true),
        ("Forest-quixel/BushTwig_01/Bush_Twig_01_Var3_Prefab", "TwigBush_3", false),
        ("Forest-quixel/BushTwig_01/Bush_Twig_01_Var4_Prefab", "TwigBush_4", false),
        ("Megascans/Plants/Bush_b/Bush_b1_4x4x4_PF", "DeadBush_4", false),
        ("Megascans/Plants/Bush_b/Bush_b1_6x8x6_PF", "DeadBush_6", false),
    };

    // Source prefabs with wrong material slots, by mesh slot, from the Materials folder next to the prefab.
    // Juniper Var3 draws its trunk with the leaf material, and Var4 has no material at all.
    static readonly Dictionary<string, string[]> BushMaterialFixes = new Dictionary<string, string[]>
    {
        ["Juniper_Bush_01_Var3"] = new[] { "Juniper_Bush_01_Trunk", "Juniper_Bush_01" },
        ["Juniper_Bush_01_Var4"] = new[] { "Juniper_Bush_01" },
    };

    // The sample bushes, with their own green or dry textures.
    static readonly (string source, string name)[] SampleBushPrefabs =
    {
        ("Bush_A", "SmallBush_A"),
        ("Bush_B", "SmallBush_B"),
        ("BushDry_A", "SmallBushDry_A"),
        ("BushDry_B", "SmallBushDry_B"),
    };

    [MenuItem("Farm Land/Step 11 - Bush Assets")]
    public static void BuildBushAssets()
    {
        RequireEditMode();
        EnsureFolder("Assets/Farm_land/Prefabs");
        EnsureFolder(BushesFolder);
        EnsureFolder(UrpMaterialsFolder);

        BushMaterialsBuilt.Clear();
        foreach (var bush in BushPrefabs)
            BuildBushPrefab(bush.source, bush.name, bush.dry);
        foreach (var bush in SampleBushPrefabs)
            BuildSampleBushPrefab(bush.source, bush.name);
        AssetDatabase.SaveAssets();
    }

    static readonly Dictionary<string, Material> BushMaterialsBuilt = new Dictionary<string, Material>();

    /// <summary>
    /// Copies a Quixel bush into a new prefab whose root has no rotation or scale, since terrain trees ignore
    /// the root's. The source roots tilt and scale their bushes, so that goes into each LOD's own transform.
    /// </summary>
    static void BuildBushPrefab(string source, string name, bool dry)
    {
        GameObject sourcePrefab = LoadRequired<GameObject>($"{QuixelRoot}/{source}.prefab");
        GameObject copy = Object.Instantiate(sourcePrefab);
        var root = new GameObject(name);
        try
        {
            BuildBushLods(source, sourcePrefab.name, copy, root, dry);
            PrefabUtility.SaveAsPrefabAsset(root, $"{BushesFolder}/{name}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(copy);
            Object.DestroyImmediate(root);
        }
    }

    static void BuildBushLods(string source, string prefabName, GameObject copy, GameObject root, bool dry)
    {
        copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        LODGroup group = root.AddComponent<LODGroup>();
        LOD[] lods = copy.GetComponent<LODGroup>().GetLODs();
        for (int i = 0; i < lods.Length; i++)
        {
            var renderers = new List<Renderer>();
            foreach (Renderer sourceRenderer in lods[i].renderers)
            {
                if (sourceRenderer == null)
                    continue;
                var child = new GameObject(sourceRenderer.name);
                child.transform.SetParent(root.transform, false);
                child.transform.SetPositionAndRotation(sourceRenderer.transform.position, sourceRenderer.transform.rotation);
                child.transform.localScale = sourceRenderer.transform.lossyScale;
                child.AddComponent<MeshFilter>().sharedMesh = sourceRenderer.GetComponent<MeshFilter>().sharedMesh;

                Material[] materials = BushSourceMaterials(source, prefabName, sourceRenderer);
                for (int m = 0; m < materials.Length; m++)
                    materials[m] = BuildBushMaterial(materials[m], dry);
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderers.Add(renderer);
            }
            lods[i].renderers = renderers.ToArray();
        }

        lods[lods.Length - 1].screenRelativeTransitionHeight = Mathf.Min(lods[lods.Length - 1].screenRelativeTransitionHeight, BushCullHeight);
        group.SetLODs(lods);
        group.RecalculateBounds();
    }

    static Material[] BushSourceMaterials(string source, string prefabName, Renderer renderer)
    {
        if (!BushMaterialFixes.TryGetValue(prefabName, out string[] names))
            return renderer.sharedMaterials;
        string folder = $"{QuixelRoot}/{System.IO.Path.GetDirectoryName(source)}/Materials";
        var materials = new Material[names.Length];
        for (int i = 0; i < names.Length; i++)
            materials[i] = LoadRequired<Material>($"{folder}/{names[i]}.mat");
        return materials;
    }

    // Opaque source materials are wood. The rest are leaves or twigs with cut-out cards.
    static Material BuildBushMaterial(Material source, bool dry)
    {
        bool cutout = SavedFloat(source, "_AlphaCutoffEnable", 0f) > 0.5f;
        string path = $"{UrpMaterialsFolder}/Bush_{source.name}{(dry && cutout ? "_Dry" : "")}.mat";
        if (BushMaterialsBuilt.TryGetValue(path, out Material built))
            return built;

        Texture baseMap = SavedTexture(source, "_BaseColorMap");
        ConfigureBushTexture(baseMap, cutout);
        Material material;
        if (cutout)
        {
            Color tint = Color.white * BushFoliageBrightness[source.name];
            if (dry)
                tint *= BushDryTint;
            tint.a = 1f;
            material = BuildFoliageMaterial(path, baseMap, tint, SavedFloat(source, "_AlphaCutoff", TreeCutoff), 0f, BushNormalUp, 1f, 0f, TreeFlutter);
            material.SetFloat("_Desaturate", dry ? BushDryDesaturate : 0f);
        }
        else
        {
            material = BuildBarkMaterial(
                path,
                baseMap,
                SavedTexture(source, "_NormalMap"),
                SavedTexture(source, "_MaskMap"),
                Mathf.Min(SavedFloat(source, "_NormalScale", 1f), TreeMaxNormalScale),
                Mathf.Min(SavedFloat(source, "_Smoothness", 0f) * 0.5f, TreeMaxSmoothness));
        }
        EditorUtility.SetDirty(material);
        BushMaterialsBuilt[path] = material;
        return material;
    }

    // Cut-out cards keep their coverage in the mips, so far bushes don't thin out.
    static void ConfigureBushTexture(Texture texture, bool cutout)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
        if (!cutout || (importer.mipMapsPreserveCoverage && importer.alphaIsTransparency))
            return;
        importer.mipMapsPreserveCoverage = true;
        importer.alphaTestReferenceValue = TreeCutoff;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    /// <summary>A small sample bush, scaled up, in a one-LOD group so it culls like the others.</summary>
    static void BuildSampleBushPrefab(string source, string name)
    {
        const string baseMapProperty = "Texture2D_E1B0D043";
        const string cutoffProperty = "Vector1_a5b8b09028ce49a39f4d090894c89e22";

        GameObject sourcePrefab = LoadRequired<GameObject>($"{SampleBushesFolder}/{source}.prefab");
        MeshRenderer sourceRenderer = sourcePrefab.GetComponent<MeshRenderer>();
        Material sourceMaterial = sourceRenderer.sharedMaterial;
        string path = $"{UrpMaterialsFolder}/Bush_Sample{sourceMaterial.name}.mat";
        if (!BushMaterialsBuilt.TryGetValue(path, out Material material))
        {
            float cutoff = sourceMaterial.HasProperty(cutoffProperty) ? sourceMaterial.GetFloat(cutoffProperty) : TreeCutoff;
            material = BuildFoliageMaterial(path, sourceMaterial.GetTexture(baseMapProperty), Color.white, cutoff, 0f, BushNormalUp, 1f, 0f, TreeFlutter);
            material.SetFloat("_Desaturate", 0f);
            BushMaterialsBuilt[path] = material;
        }

        var root = new GameObject(name);
        var child = new GameObject(source);
        child.transform.SetParent(root.transform, false);
        child.transform.localScale = Vector3.one * SampleBushScale;
        child.AddComponent<MeshFilter>().sharedMesh = sourcePrefab.GetComponent<MeshFilter>().sharedMesh;
        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        LODGroup group = root.AddComponent<LODGroup>();
        group.SetLODs(new[] { new LOD(BushCullHeight, new Renderer[] { renderer }) });
        group.RecalculateBounds();
        PrefabUtility.SaveAsPrefabAsset(root, $"{BushesFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
    }
}
