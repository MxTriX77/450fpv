using System.Collections.Generic;
using System.IO;
using Fpv450.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The LOD group's frame (p1-r01-vaz-sedan, QA 2.1's S2): through the importer's prefab step over the X-sign probe
    /// fixture, a metadata whose <c>bounds</c> carry a <c>lod</c> box (the object's own, without the scatter round it)
    /// centres the LOD group on that box and sizes it by its longest side; without one, the bounds serve. Everything is
    /// written under <see cref="Folder"/>, deleted before and after.
    /// </summary>
    public sealed class LodGroupFrameTests
    {
        const string Parent = "Assets/_Project/Tests/EditMode";
        const string Folder = Parent + "/ThrowawayLodFrame";
        const string FixturePath = "Assets/_Project/Tests/EditMode/Fixtures/xsign_probe.fbx";

        // A 6 x 1.5 x 4 m bounds (a car and its scatter), one LOD culled at 50 m; LOD is replaced per build.
        const string Metadata = @"{
  ""schema"": 1, ""id"": ""T00"", ""variant"": ""VARIANT"", ""generator"": ""test"",
  ""params_sha256"": ""0"", ""units"": ""m"", ""frame"": ""unity-lh-yup"",
  ""bounds"": { ""min"": [0.0, 0.0, 0.0], ""max"": [6.0, 1.5, 4.0]LOD },
  ""lods"": [ { ""mesh"": ""xsign_probe"", ""tris"": 12, ""switch_distance_m"": 0.0, ""thin_wire"": false } ],
  ""micro"": [],
  ""cull_distance_m"": 50.0,
  ""materials"": [ { ""slot"": ""probe"", ""visual"": ""probe"", ""physics"": ""steel"" } ],
  ""collision"": [ { ""shape"": ""box"", ""center"": [0.15, 0.05, 0.1], ""size"": [0.3, 0.1, 0.2],
                   ""rotation"": [0.0, 0.0, 0.0, 1.0], ""material"": ""steel"", ""landable"": false } ],
  ""gaps"": [], ""wind_volumes"": [], ""ports"": [], ""wires"": [], ""holes"": [], ""textures"": [], ""snag"": []
}
";
        // The car's own 4.0 x 1.4 x 1.6 m box inside them.
        const string Lod = @", ""lod"": { ""min"": [0.5, 0.0, 0.5], ""max"": [4.5, 1.4, 2.1] }";

        [SetUp]
        public void SetUp()
        {
            DeleteThrowaway();
            AssetDatabase.CreateFolder(Parent, "ThrowawayLodFrame");
        }

        [TearDown]
        public void TearDown() => DeleteThrowaway();

        static void DeleteThrowaway()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.DeleteAsset(Folder);
        }

        static LODGroup Build(string variant, string lod)
        {
            string metaPath = $"{Folder}/{variant}.assetmeta.json";
            File.WriteAllText(FullPath(metaPath), Metadata.Replace("VARIANT", variant).Replace("LOD", lod));
            AssetDatabase.ImportAsset(metaPath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(metaPath);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(FixturePath), Is.Not.Null, $"{FixturePath} is missing: run tools/blender/tests/make_xsign_probe.py in Blender");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, $"{Folder}/{variant}_probe.mat");

            string prefabPath = $"{Folder}/{variant}.prefab";
            CatalogImporter.EnsurePrefab(prefabPath, variant, Json.Obj(Json.Parse(source.text), metaPath), source, FixturePath,
                new Dictionary<string, Material> { ["probe"] = material });
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<LODGroup>();
        }

        [Test]
        public void LodBox_CentresAndSizesTheGroup()
        {
            LODGroup group = Build("framed", Lod);
            Assert.That(Vector3.Distance(group.localReferencePoint, new Vector3(2.5f, 0.7f, 1.3f)), Is.LessThan(1e-5f), "the car's centre, not its scatter's");
            Assert.That(group.size, Is.EqualTo(4.0f).Within(1e-5f), "the car's longest side");
        }

        [Test]
        public void WithoutALodBox_TheBoundsServe()
        {
            LODGroup group = Build("plain", "");
            Assert.That(Vector3.Distance(group.localReferencePoint, new Vector3(3.0f, 0.75f, 2.0f)), Is.LessThan(1e-5f));
            Assert.That(group.size, Is.EqualTo(6.0f).Within(1e-5f));
        }

        static string FullPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }
}
