using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// A cylinder primitive through the catalog importer (p1-pipeline-hardening, "Cylinder in metadata"): a
    /// throwaway metadata with one cylinder, radius 0.1 m and height 0.4 m rotated 90° about Z, over the X-sign
    /// probe fixture as its LOD mesh. The importer must build the prefab with a convex MeshCollider whose mesh
    /// matches the cylinder within 5 mm, saved as an asset so a second build with the same inputs changes no file.
    /// Everything it writes goes under <see cref="Folder"/>, deleted before and after.
    /// </summary>
    public sealed class CylinderColliderTests
    {
        const string Folder = "Assets/_Project/Tests/EditMode/Throwaway";
        const string FixturePath = "Assets/_Project/Tests/EditMode/Fixtures/xsign_probe.fbx";
        const string BrickMaterialPath = "Assets/_Project/Art/Materials/brick_red.mat";
        const string Variant = "cylinder_probe";
        const float Radius = 0.1f, Height = 0.4f;
        const float Tolerance = 0.005f;
        static readonly Vector3 Centre = new Vector3(0.15f, 0.05f, 0.1f);
        static readonly Quaternion Rotation = Quaternion.AngleAxis(90f, Vector3.forward);

        // The numbers above, as metadata schema 1 (setup-asset-pipeline design decision 6).
        const string Metadata = @"{
  ""schema"": 1, ""id"": ""T00"", ""variant"": ""cylinder_probe"", ""generator"": ""test"",
  ""params_sha256"": ""0"", ""units"": ""m"", ""frame"": ""unity-lh-yup"",
  ""bounds"": { ""min"": [0.0, 0.0, 0.0], ""max"": [0.3, 0.1, 0.2] },
  ""lods"": [ { ""mesh"": ""xsign_probe"", ""tris"": 12, ""switch_distance_m"": 0.0 } ],
  ""micro"": [],
  ""cull_distance_m"": 50.0,
  ""materials"": [ { ""slot"": ""probe"", ""visual"": ""brick_red"", ""physics"": ""masonry"" } ],
  ""collision"": [ { ""shape"": ""cylinder"", ""center"": [0.15, 0.05, 0.1], ""radius"": 0.1, ""height"": 0.4,
                   ""rotation"": [0.0, 0.0, 0.7071068, 0.7071068], ""material"": ""masonry"", ""landable"": false } ],
  ""gaps"": [], ""wind_volumes"": [], ""snag"": []
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
        public void Cylinder_BecomesAConvexMeshColliderWithinFiveMillimetres()
        {
            string prefabPath = BuildThrowawayPrefab();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, "the prefab built");
            Collider[] colliders = prefab.GetComponentsInChildren<Collider>();
            Assert.That(colliders.Length, Is.EqualTo(1));
            var collider = colliders[0] as MeshCollider;
            Assert.That(collider, Is.Not.Null, "the one collider is a MeshCollider");
            Assert.That(collider.convex, Is.True);
            AssertWithin(collider.transform.localPosition, Centre, Tolerance, "collider centre vs metadata");
            Assert.That(Quaternion.Angle(collider.transform.localRotation, Rotation), Is.LessThan(0.01f), "collider rotation vs metadata");

            Mesh mesh = collider.sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(mesh), Does.StartWith(Folder + "/"), "the mesh is an asset beside the prefab");
            Assert.That(AssetDatabase.GetAssetPath(mesh), Does.EndWith(".asset"), "the mesh is its own asset file");
            AssertWithin(mesh.bounds.center, Vector3.zero, Tolerance, "mesh bounds centre");
            AssertWithin(mesh.bounds.size, new Vector3(2f * Radius, Height, 2f * Radius), Tolerance, "mesh bounds size");
            Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(255), "a PhysX convex hull takes 255 vertices at most");
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                Assert.That(Mathf.Abs(DistanceToCylinder(vertices[i])), Is.LessThanOrEqualTo(Tolerance), $"vertex {i} {vertices[i]} off the cylinder surface");
        }

        [Test]
        public void Cylinder_SecondBuildWithTheSameInputsChangesNoFile()
        {
            BuildThrowawayPrefab();
            Dictionary<string, byte[]> first = Snapshot();
            Assert.That(first.Keys.Count(name => name.EndsWith(".asset")), Is.EqualTo(1), "one mesh asset beside the prefab");

            BuildThrowawayPrefab();
            Dictionary<string, byte[]> second = Snapshot();
            Assert.That(second.Keys, Is.EquivalentTo(first.Keys), "the same files");
            foreach (KeyValuePair<string, byte[]> file in first)
                Assert.That(second[file.Key], Is.EqualTo(file.Value), $"{file.Key} changed");
        }

        /// <summary>Writes the metadata under the throwaway folder and runs the importer's prefab step over the probe FBX; the prefab path.</summary>
        static string BuildThrowawayPrefab()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(Folder).Replace('\\', '/'), Path.GetFileName(Folder));
            string metaPath = $"{Folder}/{Variant}.assetmeta.json";
            File.WriteAllText(FullPath(metaPath), Metadata);
            AssetDatabase.ImportAsset(metaPath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(metaPath);
            Assert.That(source, Is.Not.Null, "the metadata imported as a TextAsset");
            var brick = AssetDatabase.LoadAssetAtPath<Material>(BrickMaterialPath);
            Assert.That(brick, Is.Not.Null, $"{BrickMaterialPath} is missing: run pwsh tools/build_asset.ps1 K01");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(FixturePath), Is.Not.Null, $"{FixturePath} is missing: run tools/blender/tests/make_xsign_probe.py in Blender");

            string prefabPath = $"{Folder}/{Variant}.prefab";
            CatalogImporter.EnsurePrefab(prefabPath, Variant, Json.Obj(Json.Parse(source.text), metaPath), source, FixturePath,
                new Dictionary<string, Material> { ["probe"] = brick });
            AssetDatabase.SaveAssets();
            return prefabPath;
        }

        static Dictionary<string, byte[]> Snapshot() =>
            Directory.GetFiles(FullPath(Folder)).ToDictionary(file => Path.GetFileName(file), file => File.ReadAllBytes(file));

        static string FullPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        /// <summary>The signed distance from a point to the closed cylinder surface, in the mesh's own frame (axis +Y, centred).</summary>
        static float DistanceToCylinder(Vector3 p)
        {
            float radial = Mathf.Sqrt(p.x * p.x + p.z * p.z) - Radius;
            float axial = Mathf.Abs(p.y) - Height / 2f;
            float outside = Mathf.Sqrt(Mathf.Max(radial, 0f) * Mathf.Max(radial, 0f) + Mathf.Max(axial, 0f) * Mathf.Max(axial, 0f));
            return Mathf.Min(Mathf.Max(radial, axial), 0f) + outside;
        }

        static void AssertWithin(Vector3 actual, Vector3 expected, float tolerance, string what)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), $"{what} x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), $"{what} y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance), $"{what} z");
        }
    }
}
