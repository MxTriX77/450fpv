using System.IO;
using Fpv450.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The layered house recipes (p1-h01-adobe-house tasks 1.1 and 1.2): <c>facade_h01.json</c> and
    /// <c>interior_h01.json</c> are tiled-set recipes whose own material, what a slot without its damage mask draws, is
    /// the bare adobe on URP/Lit with a constant smoothness, and whose <c>layers</c> lie over their <c>base</c> (the
    /// whitewash, the interior's blue). The base and every tiled layer build as a plain set with its normal map and
    /// packed mask, the soot and dirt as one-colour palettes, so the layered shader finds each one's maps (how the
    /// importer composes them is <see cref="LayeredSurfaceMaterialTests"/>'s). The materials are written under
    /// <see cref="Folder"/>, deleted before and after.
    /// </summary>
    public sealed class LayeredRecipeTests
    {
        const string Folder = "Assets/_Project/Tests/EditMode/ThrowawayLayered";
        const string ManifestFile = "tools/assets/manifest.json";
        const string AdobeDiffuse = "Assets/_Project/Art/ThirdParty/polyhaven/rough_plaster_brick_02/rough_plaster_brick_02_diff_2k.png";

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

        [TestCase("facade_h01", "whitewash")]
        [TestCase("interior_h01", "whitewash_blue")]
        public void LayeredRecipe_BuildsItsAdobeFallbackItsBaseAndEachLayer(string recipe, string whitewash)
        {
            Material fallback = Build(recipe);
            Assert.That(fallback.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), "the fallback, what an unmasked slot draws");
            Assert.That(AssetDatabase.GetAssetPath(fallback.GetTexture("_BaseMap")), Is.EqualTo(AdobeDiffuse), "the bare adobe");
            Assert.That(fallback.GetTexture("_MetallicGlossMap"), Is.Null, "a constant smoothness, no duplicate mask");
            Assert.That(fallback.GetFloat("_Smoothness"), Is.EqualTo(0.1f));

            foreach (string layer in new[] { whitewash, "clay_render", "base_band", "adobe" })
            {
                Material material = Build(layer);
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), layer);
                Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null, $"{layer}: its set's diffuse");
                Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null, $"{layer}: its set's normal map");
                Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_MetallicGlossMap")),
                    Is.EqualTo($"Assets/_Project/Art/Materials/{layer}_mask.png"), $"{layer}: its packed mask");
            }
            foreach (string palette in new[] { "wall_soot", "wall_dirt" })
            {
                Material material = Build(palette);
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), palette);
                Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")),
                    Is.EqualTo($"Assets/_Project/Art/Materials/{palette}_albedo.png"), $"{palette}: its derived colour");
                Assert.That(material.GetTexture("_BumpMap"), Is.Null, $"{palette}: flat colour, no normal map");
            }
        }
    }
}
