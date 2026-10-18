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
    /// LOD and an impostor LOD switching at 30 m before a 100 m cull, its 1k atlas a throwaway PNG beside it. The
    /// prefab's LOD group must end with the card set before the cull: the mesh LOD until the switch, the card from the
    /// switch to the cull, the card a camera-facing set of strips reading the atlas. Everything it writes goes under
    /// <see cref="Folder"/>, deleted before and after.
    /// </summary>
    public sealed class ImpostorLodTests
    {
        const string Folder = "Assets/_Project/Tests/EditMode/ThrowawayImpostor";
        const string FixturePath = "Assets/_Project/Tests/EditMode/Fixtures/xsign_probe.fbx";
        const string BrickMaterialPath = "Assets/_Project/Art/Materials/brick_red.mat";
        const string Variant = "impostor_probe";
        const string Card = Variant + "_impostor";
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
    { ""mesh"": ""xsign_probe"", ""tris"": 12, ""switch_distance_m"": 0.0 },
    { ""mesh"": ""impostor_probe_impostor"", ""tris"": 2, ""switch_distance_m"": 30.0,
      ""impostor"": { ""atlas"": ""impostor_probe_impostor.png"", ""size_px"": [1024, 1024], ""views"": 8, ""columns"": 4,
                    ""rows"": 2, ""elevation_deg"": 15.0,
                    ""card"": { ""width_m"": 0.4, ""height_m"": 0.6, ""bottom_m"": -0.05 },
                    ""crown"": { ""centre"": [0.15, 0.3, 0.1], ""radii_m"": [0.2, 0.25], ""up"": 0.25, ""occlusion"": 0.75 },
                    ""inputs_sha256"": ""0000000000000000000000000000000000000000000000000000000000000000"",
                    ""atlas_sha256"": ""0000000000000000000000000000000000000000000000000000000000000000"" } }
  ],
  ""cull_distance_m"": 100.0,
  ""materials"": [ { ""slot"": ""probe"", ""visual"": ""brick_red"", ""physics"": ""masonry"" } ],
  ""collision"": [], ""gaps"": [], ""wind_volumes"": [], ""snag"": []
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
            Assert.That(material.GetVector("_ImpostorGrid"), Is.EqualTo(new Vector4(8f, 4f, 2f, 0f)), "8 views on 4 x 2 cells");
            Assert.That(material.GetVector("_ImpostorCard"), Is.EqualTo(new Vector4(0.4f, 0.6f, -0.05f, 0f)), "the card's width, height and bottom");
            Assert.That(material.GetVector("_ImpostorCrown"), Is.EqualTo(new Vector4(0.15f, 0.3f, 0.1f, 0.25f)), "the crown's centre and up share");
            Assert.That(material.GetVector("_ImpostorRadii"), Is.EqualTo(new Vector4(0.2f, 0.25f, 0f, 0f)), "the crown's radii");
            Assert.That(material.IsKeywordEnabled("_OCCLUSIONMAP"), Is.True);
            Assert.That(material.GetFloat("_OcclusionStrength"), Is.EqualTo(0.25f), "the black occlusion map at 1 - occlusion: the ambient light times 0.75");

            var importer = (TextureImporter)AssetImporter.GetAtPath($"{Folder}/{Card}.png");
            Assert.That(importer.sRGBTexture, Is.True, "the atlas holds albedo");
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipMapsPreserveCoverage, Is.True, "the card keeps its coverage at distance");
            Assert.That(importer.alphaTestReferenceValue, Is.EqualTo(0.5f));
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
            string atlasPath = $"{Folder}/{Card}.png";
            if (!File.Exists(FullPath(atlasPath)))
            {
                var atlas = new Texture2D(AtlasPx, AtlasPx, TextureFormat.RGBA32, false);
                var pixels = new Color32[AtlasPx * AtlasPx];
                for (int y = 0; y < AtlasPx; y++)
                    for (int x = 0; x < AtlasPx; x++)
                        pixels[y * AtlasPx + x] = x % 256 is > 64 and < 192 && y % 512 is > 16 and < 400
                            ? new Color32(70, 90, 40, 255) : new Color32(0, 0, 0, 0);
                atlas.SetPixels32(pixels);
                File.WriteAllBytes(FullPath(atlasPath), atlas.EncodeToPNG());
                Object.DestroyImmediate(atlas);
                AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
            }
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
