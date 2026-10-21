using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The impostor LOD through the catalog importer (p1-v01-locust design decision 3; asset-pipeline "Baked impostor
    /// LOD", scenario "Impostor built and switched"): a throwaway metadata over the X-sign probe fixture with one mesh
    /// LOD and an impostor LOD switching at 30 m before a 100 m cull, its 1k albedo, normal and smoothness atlases
    /// throwaway PNGs beside it. The prefab's LOD group must end with the card set before the cull: the mesh LOD until
    /// the switch, the card from the switch to the cull, the card a camera-facing set of strips reading the atlases. Everything it
    /// writes goes under <see cref="Folder"/>, deleted before and after.
    /// </summary>
    public sealed class ImpostorLodTests
    {
        const string Folder = "Assets/_Project/Tests/EditMode/ThrowawayImpostor";
        const string FixturePath = "Assets/_Project/Tests/EditMode/Fixtures/xsign_probe.fbx";
        const string BrickMaterialPath = "Assets/_Project/Art/Materials/brick_red.mat";
        const string Variant = "impostor_probe";
        const string Card = Variant + "_impostor";
        const string Normals = Card + "_normal";
        const string Smoothness = Card + "_smoothness";
        const float SwitchM = 30f, CullM = 100f;
        const int AtlasPx = 1024;          // the spec's 1k atlas
        const float VerticalFovDeg = 60f;  // the importer's distance-to-height conversion
        const float BoundsSize = 0.3f;     // the longest side of the metadata bounds below

        // Metadata schema 1 with the impostor LOD as tools/blender/lib/impostor.py writes it.
        const string Metadata = @"{
  ""schema"": 1, ""id"": ""T01"", ""variant"": ""impostor_probe"", ""generator"": ""test"",
  ""params_sha256"": ""0"", ""units"": ""m"", ""frame"": ""unity-lh-yup"",
  ""bounds"": { ""min"": [0.0, 0.0, 0.0], ""max"": [0.3, 0.1, 0.2] },
  ""lods"": [
    { ""mesh"": ""xsign_probe"", ""tris"": 12, ""switch_distance_m"": 0.0, ""thin_wire"": false },
    { ""mesh"": ""impostor_probe_impostor"", ""tris"": 2, ""switch_distance_m"": 30.0, ""thin_wire"": false,
      ""impostor"": { ""atlas"": ""impostor_probe_impostor.png"", ""normals"": ""impostor_probe_impostor_normal.png"",
                    ""smoothness"": ""impostor_probe_impostor_smoothness.png"", ""size_px"": [1024, 1024], ""views"": 8, ""columns"": 4, ""rows"": 2, ""elevation_deg"": 15.0,
                    ""card"": { ""width_m"": 0.4, ""height_m"": 0.6, ""bottom_m"": -0.05 },
                    ""inputs_sha256"": ""0000000000000000000000000000000000000000000000000000000000000000"",
                    ""atlas_sha256"": ""0000000000000000000000000000000000000000000000000000000000000000"",
                    ""normals_sha256"": ""0000000000000000000000000000000000000000000000000000000000000000"",
                    ""smoothness_sha256"": ""0000000000000000000000000000000000000000000000000000000000000000"" } }
  ],
  ""micro"": [],
  ""cull_distance_m"": 100.0,
  ""materials"": [ { ""slot"": ""probe"", ""visual"": ""brick_red"", ""physics"": ""masonry"" } ],
  ""collision"": [], ""gaps"": [], ""wind_volumes"": [], ""ports"": [], ""wires"": [], ""holes"": [], ""snag"": []
}
";

        [SetUp]
        public void SetUp() => DeleteThrowaway();

        [TearDown]
        public void TearDown() => DeleteThrowaway();

        static void DeleteThrowaway()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void LodGroup_EndsWithTheCardSetBeforeTheCull()
        {
            LODGroup group = Prefab().GetComponent<LODGroup>();
            Assert.That(group.size, Is.EqualTo(BoundsSize).Within(1e-6f), "the metadata's size, not the card's, sets the switch distances");
            AssertWithin(group.localReferencePoint, new Vector3(0.15f, 0.05f, 0.1f), "the metadata's centre");
            LOD[] lods = group.GetLODs();
            Assert.That(lods.Length, Is.EqualTo(2), "the mesh LOD and the impostor");
            Assert.That(lods[0].renderers.Single().GetComponent<MeshFilter>().sharedMesh.name, Is.EqualTo("xsign_probe"));
            Renderer card = lods[1].renderers.Single();
            Assert.That(card.name, Is.EqualTo(Card), "the last LOD is the impostor's card set");
            Assert.That(lods[0].screenRelativeTransitionHeight, Is.EqualTo(RelativeHeight(SwitchM)).Within(1e-6f), "the mesh LOD gives way at the switch");
            Assert.That(lods[1].screenRelativeTransitionHeight, Is.EqualTo(RelativeHeight(CullM)).Within(1e-6f), "the card shows until the cull");

            Mesh mesh = card.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.name, Is.EqualTo(Card));
            Assert.That(mesh.triangles.Length / 3, Is.EqualTo(2), "one quad, the metadata's 2 triangles");
            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            Assert.That(AssetDatabase.GetAssetPath(mesh), Is.EqualTo($"{Folder}/{Variant}_impostor.asset"), "the card mesh is an asset beside the prefab");
            AssertWithin(mesh.bounds.center, new Vector3(0f, 0.25f, 0f), "card bounds centre: bottom plus half the height");
            AssertWithin(mesh.bounds.size, new Vector3(0.4f, 0.6f, 0.4f), "card bounds: what the card sweeps as it turns");

            Material material = card.sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("Fpv450/Impostor"));
            var atlas = (Texture2D)material.GetTexture("_BaseMap");
            Assert.That(AssetDatabase.GetAssetPath(atlas), Is.EqualTo($"{Folder}/{Card}.png"), "the atlas beside the metadata");
            Assert.That((atlas.width, atlas.height), Is.EqualTo((AtlasPx, AtlasPx)), "a 1k atlas");
            var normals = (Texture2D)material.GetTexture("_BumpMap");
            Assert.That(AssetDatabase.GetAssetPath(normals), Is.EqualTo($"{Folder}/{Normals}.png"), "the normal atlas beside the metadata");
            Assert.That((normals.width, normals.height), Is.EqualTo((AtlasPx, AtlasPx)), "a 1k normal atlas");
            var smoothness = (Texture2D)material.GetTexture("_SmoothnessMap");
            Assert.That(AssetDatabase.GetAssetPath(smoothness), Is.EqualTo($"{Folder}/{Smoothness}.png"), "the smoothness atlas beside the metadata");
            Assert.That((smoothness.width, smoothness.height), Is.EqualTo((AtlasPx, AtlasPx)), "a 1k smoothness atlas");
        }

        [Test]
        public void CardSet_ReadsTheAtlasClippedAndFacesTheCamera()
        {
            Material material = Prefab().GetComponent<LODGroup>().GetLODs()[1].renderers.Single().sharedMaterial;
            Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.True);
            Assert.That(material.GetFloat("_Cutoff"), Is.EqualTo(0.5f));
            Assert.That(material.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Off));
            Assert.That(material.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF"), Is.True);
            Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.AlphaTest));
            Assert.That(material.GetVector("_ImpostorGrid"), Is.EqualTo(new Vector4(8f, 4f, 2f, 15f)), "8 views on 4 x 2 cells, 15 deg up");
            Assert.That(material.GetVector("_ImpostorCard"), Is.EqualTo(new Vector4(0.4f, 0.6f, -0.05f, 0f)), "the card's width, height and bottom");
            Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.False, "the shader reads the normal atlas itself");
            Assert.That(material.enableInstancing, Is.True, "a belt's cards draw instanced");
            Assert.That(material.IsKeywordEnabled("_OCCLUSIONMAP"), Is.False, "the shader takes the occlusion per texel from the normal atlas's alpha");

            var importer = (TextureImporter)AssetImporter.GetAtPath($"{Folder}/{Card}.png");
            Assert.That(importer.sRGBTexture, Is.True, "the atlas holds albedo");
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipMapsPreserveCoverage, Is.True, "the card keeps its coverage at distance");
            Assert.That(importer.alphaTestReferenceValue, Is.EqualTo(0.5f));
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath($"{Folder}/{Normals}.png");
            Assert.That(normalImporter.sRGBTexture, Is.False, "the normal atlas is linear data");
            Assert.That(normalImporter.textureType, Is.EqualTo(TextureImporterType.Default), "not a tangent-space normal map");
            Assert.That(normalImporter.mipmapEnabled, Is.False, "read at full resolution");
            Assert.That(normalImporter.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ), "the means' lengths, the spread, kept (task 1.2n)");
            Assert.That(normalImporter.filterMode, Is.EqualTo(FilterMode.Point), "each pixel one texel's spread, not a blend of two (task 1.2n)");
            Assert.That(normalImporter.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), "its alpha, the occlusion per texel, kept");
            Assert.That(normalImporter.alphaIsTransparency, Is.False, "the alpha is data, not coverage");
            var smoothnessImporter = (TextureImporter)AssetImporter.GetAtPath($"{Folder}/{Smoothness}.png");
            Assert.That(smoothnessImporter.sRGBTexture, Is.False, "the smoothness atlas is linear data");
            Assert.That(smoothnessImporter.textureType, Is.EqualTo(TextureImporterType.SingleChannel), "one channel");
            var channel = new TextureImporterSettings();
            smoothnessImporter.ReadTextureSettings(channel);
            Assert.That(channel.singleChannelComponent, Is.EqualTo(TextureImporterSingleChannelComponent.Red), "the shader reads R");
            Assert.That(smoothnessImporter.mipmapEnabled, Is.False, "read at full resolution, as the normal atlas");
            Assert.That(smoothnessImporter.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ));
            Assert.That(smoothnessImporter.filterMode, Is.EqualTo(FilterMode.Point), "each pixel its texel's surfaces, as the normal atlas (task 1.6b)");
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(1f), "the smoothness atlas's texels unscaled, as a masked material's");
        }

        [Test]
        public void CardAndLeafShaders_CompileWithTheirCoverageReads()
        {
            // CoverageAlpha.hlsl's two SampleAlbedoAlpha forms (p1-v01-locust task 1.2l): the impostor reads its colour
            // with its alpha (FPV450_COVERAGE_COLOUR), the leaf its colour at the pixel's mip.
            foreach (string name in new[] { "Fpv450/Impostor", "Fpv450/Leaf" })
            {
                Shader shader = Shader.Find(name);
                Assert.That(shader, Is.Not.Null, name);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, $"{name} compiles");
            }
        }

        [Test]
        public void CardSet_SecondBuildWithTheSameInputsChangesNoFile()
        {
            Prefab();
            Dictionary<string, byte[]> first = Snapshot();
            Prefab();
            Dictionary<string, byte[]> second = Snapshot();
            Assert.That(second.Keys, Is.EquivalentTo(first.Keys), "the same files");
            foreach (KeyValuePair<string, byte[]> file in first)
                Assert.That(second[file.Key], Is.EqualTo(file.Value), $"{file.Key} changed");
        }

        static float RelativeHeight(float distance) =>
            BoundsSize / (2f * distance * Mathf.Tan(VerticalFovDeg * 0.5f * Mathf.Deg2Rad));

        /// <summary>Writes the metadata and a 1k atlas under the throwaway folder, runs the importer's prefab step over the probe FBX and loads the prefab.</summary>
        static GameObject Prefab()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(Folder).Replace('\\', '/'), Path.GetFileName(Folder));
            WriteAtlas($"{Folder}/{Card}.png", TextureFormat.RGBA32, (x, y) => x % 256 is > 64 and < 192 && y % 512 is > 16 and < 400
                ? new Color32(70, 90, 40, 255) : new Color32(0, 0, 0, 0));
            WriteAtlas($"{Folder}/{Normals}.png", TextureFormat.RGBA32, (x, y) => new Color32(128, 128, 255, 191)); // occlusion 0.75
            WriteAtlas($"{Folder}/{Smoothness}.png", TextureFormat.RGBA32, (x, y) => x < 512 ? new Color32(51, 51, 51, 255) : new Color32(24, 24, 24, 255)); // a leaf's 0.2, a bark's 0.096
            string metaPath = $"{Folder}/{Variant}.assetmeta.json";
            File.WriteAllText(FullPath(metaPath), Metadata);
            AssetDatabase.ImportAsset(metaPath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(metaPath);
            Assert.That(source, Is.Not.Null, "the metadata imported as a TextAsset");
            var brick = AssetDatabase.LoadAssetAtPath<Material>(BrickMaterialPath);
            Assert.That(brick, Is.Not.Null, $"{BrickMaterialPath} is missing: run pwsh tools/build_asset.ps1 K01");

            string prefabPath = $"{Folder}/{Variant}.prefab";
            CatalogImporter.EnsurePrefab(prefabPath, Variant, Json.Obj(Json.Parse(source.text), metaPath), source, FixturePath,
                new Dictionary<string, Material> { ["probe"] = brick });
            AssetDatabase.SaveAssets();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, "the prefab built");
            return prefab;
        }

        /// <summary>A throwaway 1k atlas of the given texels, written and imported once.</summary>
        static void WriteAtlas(string path, TextureFormat format, System.Func<int, int, Color32> texel)
        {
            if (File.Exists(FullPath(path)))
                return;
            var atlas = new Texture2D(AtlasPx, AtlasPx, format, false);
            var pixels = new Color32[AtlasPx * AtlasPx];
            for (int y = 0; y < AtlasPx; y++)
                for (int x = 0; x < AtlasPx; x++)
                    pixels[y * AtlasPx + x] = texel(x, y);
            atlas.SetPixels32(pixels);
            File.WriteAllBytes(FullPath(path), atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        static Dictionary<string, byte[]> Snapshot() =>
            Directory.GetFiles(FullPath(Folder)).ToDictionary(file => Path.GetFileName(file), file => File.ReadAllBytes(file));

        static string FullPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        static void AssertWithin(Vector3 actual, Vector3 expected, string what)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-5f), $"{what} x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-5f), $"{what} y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(1e-5f), $"{what} z");
        }
    }
}
