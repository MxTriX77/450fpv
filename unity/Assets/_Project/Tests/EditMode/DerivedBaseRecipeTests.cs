using System.IO;
using Fpv450.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// Recipes whose base map <c>tools/assets/derive.py</c> derives rather than the set's diffuse (p1-v01-locust,
    /// design decision 5): <c>bark_locust</c> tints its set's grooves by the cavity ("groove") and stays an opaque
    /// URP/Lit bark with the set's normal map; <c>pod_locust</c> is painted by script ("paint", no texture set) and,
    /// clipped and two-sided, is a leaf-shader card with no normal map. The materials are written under
    /// <see cref="Folder"/>, deleted before and after.
    /// </summary>
    public sealed class DerivedBaseRecipeTests
    {
        const string Folder = "Assets/_Project/Tests/EditMode/ThrowawayDerivedBase";
        const string ManifestFile = "tools/assets/manifest.json";

        [SetUp]
        public void SetUp() => DeleteThrowaway();

        [TearDown]
        public void TearDown() => DeleteThrowaway();

        static void DeleteThrowaway()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.DeleteAsset(Folder);
        }

        static Material Build(string recipe)
        {
            string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ManifestFile));
            var manifest = Json.Obj(Json.Parse(File.ReadAllText(manifestPath)), ManifestFile);
            Material material = CatalogImporter.EnsureMaterial(recipe, manifest, $"{Folder}/{recipe}.mat");
            AssetDatabase.SaveAssets();
            return material;
        }

        [Test]
        public void GrooveRecipe_TakesItsDerivedBaseAndStaysOpaqueBark()
        {
            Material material = Build("bark_locust");

            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")),
                Is.EqualTo("Assets/_Project/Art/Materials/bark_locust_base.png"), "the grooves tinted by derive.py");
            Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null, "the set's normal map");
            Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.False, "not clipped");
            Assert.That(material.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Back));
        }

        [Test]
        public void PaintedRecipe_IsAClippedCardOnItsPaintedBase()
        {
            Material material = Build("pod_locust");

            Assert.That(material.shader.name, Is.EqualTo("Fpv450/Leaf"), "clipped and two-sided: the leaf shader");
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")),
                Is.EqualTo("Assets/_Project/Art/Materials/pod_locust_base.png"), "painted by derive.py");
            Assert.That(material.GetTexture("_BumpMap"), Is.Null, "a painted map has no normal map");
            Assert.That(material.GetFloat("_Cutoff"), Is.EqualTo(0.5f));
            Assert.That(material.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Off));
        }
    }
}
