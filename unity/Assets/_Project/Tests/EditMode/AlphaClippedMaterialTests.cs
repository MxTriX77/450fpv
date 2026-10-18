using System.IO;
using Fpv450.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// An alpha-clipped two-sided recipe through the catalog importer (p1-v13-shrub, "Alpha-clipped two-sided
    /// recipes", scenario "Leaf card material"): <c>leaf_shrub.json</c> names the LeafSet with <c>alpha_clip</c> 0.5
    /// and <c>two_sided</c>. The material built from it clips at 0.5 on an opaque surface, culls no face, and reads
    /// its alpha from the derived base map (the set ships its opacity separately), whose import keeps the alpha
    /// channel. The material is written under <see cref="Folder"/>, deleted before and after.
    /// </summary>
    public sealed class AlphaClippedMaterialTests
    {
        const string Folder = "Assets/_Project/Tests/EditMode/ThrowawayMaterial";
        const string Recipe = "leaf_shrub";
        const string BasePath = "Assets/_Project/Art/Materials/leaf_shrub_base.png";
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

        [Test]
        public void LeafRecipe_ClipsAtHalfAndCullsNoFace()
        {
            string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ManifestFile));
            var manifest = Json.Obj(Json.Parse(File.ReadAllText(manifestPath)), ManifestFile);
            Material material = CatalogImporter.EnsureMaterial(Recipe, manifest, $"{Folder}/{Recipe}.mat");
            AssetDatabase.SaveAssets();

            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.True, "alpha clipping keyword");
            Assert.That(material.GetFloat("_AlphaClip"), Is.EqualTo(1f), "alpha clipping on");
            Assert.That(material.GetFloat("_Cutoff"), Is.EqualTo(0.5f), "the recipe's cutoff");
            Assert.That(material.GetFloat("_Surface"), Is.EqualTo(0f), "opaque surface with clipping, not transparent");
            Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.AlphaTest));
            Assert.That(material.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Off), "no face culled");
            Assert.That(material.doubleSidedGI, Is.True);

            var baseMap = material.GetTexture("_BaseMap") as Texture2D;
            Assert.That(baseMap, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(baseMap), Is.EqualTo(BasePath),
                "the LeafSet ships opacity separately, so the base map is the one derive.py packed");
            Assert.That(GraphicsFormatUtility.HasAlphaChannel(baseMap.graphicsFormat), Is.True, "the imported base map keeps its alpha");
            var importer = (TextureImporter)AssetImporter.GetAtPath(BasePath);
            Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), "the alpha as packed, not from grey or premultiplied");
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.sRGBTexture, Is.True, "the base map is colour");
        }
    }
}
