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
    /// recipes", scenarios "Leaf card material" and "Coverage kept at distance"): <c>leaf_shrub.json</c> names the
    /// LeafSet with <c>alpha_clip</c> 0.5 and <c>two_sided</c>. The material built from it is the leaf shader
    /// (design decision 6), clips at 0.5 on an opaque surface, culls no face, and reads its alpha from the derived
    /// base map (the set ships its opacity separately), whose import keeps the alpha channel and its coverage at the
    /// cutoff in every mip. Recipes that are not clipped and two-sided keep URP/Lit, and so does the leaf recipe's
    /// far-card recipe (p1-v01-locust task 1.2j), its maps and settings with back faces culled and no normal map. The
    /// materials are written under <see cref="Folder"/>, deleted before and after.
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

        static Material Build(string recipe)
        {
            string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ManifestFile));
            var manifest = Json.Obj(Json.Parse(File.ReadAllText(manifestPath)), ManifestFile);
            Material material = CatalogImporter.EnsureMaterial(recipe, manifest, $"{Folder}/{recipe}.mat");
            AssetDatabase.SaveAssets();
            return material;
        }

        [Test]
        public void LeafRecipe_ClipsAtHalfAndCullsNoFace()
        {
            Material material = Build(Recipe);

            Assert.That(material.shader.name, Is.EqualTo("Fpv450/Leaf"), "a clipped two-sided recipe gets the leaf shader");
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
            Assert.That(importer.alphaIsTransparency, Is.False,
                "derive.py gives the clipped texels the kept mean colour, which Unity's dilation would paint over (task 1.2l)");
            Assert.That(importer.sRGBTexture, Is.True, "the base map is colour");
        }

        [Test]
        public void LeafRecipe_BaseMapKeepsItsCoverageAtTheCutoff()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(BasePath);
            importer.mipMapsPreserveCoverage = false; // as before the importer set it; the build puts it back
            importer.alphaTestReferenceValue = 0.3f;
            importer.SaveAndReimport();

            Build(Recipe);

            importer = (TextureImporter)AssetImporter.GetAtPath(BasePath);
            Assert.That(importer.mipMapsPreserveCoverage, Is.True, "the mips keep the alpha coverage");
            Assert.That(importer.alphaTestReferenceValue, Is.EqualTo(0.5f), "at the recipe's cutoff");
        }

        [Test]
        public void FarCardRecipe_IsTheLeafMaterialOneSidedWithoutItsNormalMap()
        {
            // leaf_shrub_far.json holds only "far_card_of": "leaf_shrub" (p1-v01-locust task 1.2j): V13's far cards
            // are two faces back to back, each with normals of its own, which the leaf shader would turn round.
            Material leaf = Build(Recipe);
            Material far = Build("leaf_shrub_far");

            Assert.That(far.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), "no back face shows, so no leaf shader");
            Assert.That(far.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Back), "back faces culled");
            Assert.That(far.doubleSidedGI, Is.False);
            Assert.That(leaf.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(far.GetTexture("_BumpMap"), Is.Null, "the far card's normals are its own");
            Assert.That(far.IsKeywordEnabled("_NORMALMAP"), Is.False);
            foreach (string map in new[] { "_BaseMap", "_MetallicGlossMap", "_OcclusionMap" })
                Assert.That(far.GetTexture(map), Is.SameAs(leaf.GetTexture(map)), $"{map}: the leaf recipe's own map");
            Assert.That(AssetDatabase.GetAssetPath(far.GetTexture("_BaseMap")), Is.EqualTo(BasePath));
            Assert.That(far.GetColor("_BaseColor"), Is.EqualTo(leaf.GetColor("_BaseColor")), "the leaf recipe's tint");
            Assert.That(far.GetTextureScale("_BaseMap"), Is.EqualTo(leaf.GetTextureScale("_BaseMap")));
            Assert.That(far.GetFloat("_Smoothness"), Is.EqualTo(leaf.GetFloat("_Smoothness")));
            Assert.That(far.IsKeywordEnabled("_ALPHATEST_ON"), Is.True, "clipped as the leaves");
            Assert.That(far.GetFloat("_Cutoff"), Is.EqualTo(0.5f));
            Assert.That(far.renderQueue, Is.EqualTo((int)RenderQueue.AlphaTest));
        }

        [TestCase("bark_shrub")]
        [TestCase("brick_red")]
        public void OpaqueRecipe_KeepsUrpLit(string recipe)
        {
            Material material = Build(recipe);

            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.False, "not clipped");
            Assert.That(material.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Back), "back faces culled");
        }
    }
}
