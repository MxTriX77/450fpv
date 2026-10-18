using System.IO;
using Fpv450.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The belt_straw material as the catalog importer builds it from its <c>ground</c> recipe (p1-s04-belt-straw,
    /// "Ground recipe derived and wired"): the derived maps as the base, the cover set as the detail layer. Needs the
    /// pinned sets (<c>python tools/assets/fetch.py --all</c>) and the derived maps (<c>derive.py belt_straw</c>).
    /// </summary>
    public sealed class GroundMaterialTests
    {
        const string Materials = "Assets/_Project/Art/Materials";
        const string DetailNormalPath = "Assets/_Project/Art/ThirdParty/texturecan/ground_0018/ground_0018_normal_opengl_2k.png";
        const float SizeM = 12f, DetailTileM = 2f; // assets-src/materials/belt_straw.json

        static Material BeltStraw()
        {
            string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", "assets", "manifest.json"));
            var manifest = Json.Obj(Json.Parse(File.ReadAllText(manifestPath)), manifestPath);
            Material material = CatalogImporter.EnsureMaterial("belt_straw", manifest);
            AssetDatabase.SaveAssets();
            return material;
        }

        static Texture2D Derived(string role)
        {
            string path = $"{Materials}/belt_straw_{role}.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null, $"{path} is missing: run python tools/assets/derive.py belt_straw");
            return texture;
        }

        [Test]
        public void GroundMaterial_UsesTheDerivedMapsAsItsBase()
        {
            Material material = BeltStraw();
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(Derived("albedo")));
            Assert.That(material.GetTexture("_BumpMap"), Is.SameAs(Derived("normal")));
            Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
            Assert.That(material.GetTexture("_MetallicGlossMap"), Is.SameAs(Derived("mask")));
            Assert.That(material.GetTexture("_OcclusionMap"), Is.SameAs(Derived("mask")));
            Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
            Assert.That(material.IsKeywordEnabled("_OCCLUSIONMAP"), Is.True);
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(1f), "the mask's alpha is not scaled");
            Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(1f / SizeM, 1f / SizeM)), "one unique map over the 12 m patch");
            Assert.That(material.GetTextureOffset("_BaseMap"), Is.EqualTo(new Vector2(0.5f, 0.5f)), "the patch is centred on the origin");
        }

        [Test]
        public void GroundMaterial_CarriesTheDetailLayerAtTheTileSize()
        {
            Material material = BeltStraw();
            Assert.That(material.GetTexture("_DetailAlbedoMap"), Is.SameAs(Derived("detail_albedo")));
            var detailNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(DetailNormalPath);
            Assert.That(detailNormal, Is.Not.Null, $"{DetailNormalPath} is missing: run python tools/assets/fetch.py ground_0018");
            Assert.That(material.GetTexture("_DetailNormalMap"), Is.SameAs(detailNormal));
            Assert.That(material.IsKeywordEnabled("_DETAIL_MULX2"), Is.True);
            Assert.That(material.GetTextureScale("_DetailAlbedoMap"), Is.EqualTo(new Vector2(SizeM / DetailTileM, SizeM / DetailTileM)),
                "URP's detail UV is the base UV times this, so the detail repeats every tile_size_m");
            Assert.That(material.GetFloat("_DetailAlbedoMapScale"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_DetailNormalMapScale"), Is.EqualTo(1f));
        }

        [Test]
        public void DerivedGroundMaps_ImportByTheirRole()
        {
            BeltStraw();
            AssertImport($"{Materials}/belt_straw_albedo.png", TextureImporterType.Default, srgb: true);
            AssertImport($"{Materials}/belt_straw_detail_albedo.png", TextureImporterType.Default, srgb: true);
            AssertImport($"{Materials}/belt_straw_normal.png", TextureImporterType.NormalMap, srgb: false);
            AssertImport($"{Materials}/belt_straw_mask.png", TextureImporterType.Default, srgb: false);
            AssertImport(DetailNormalPath, TextureImporterType.NormalMap, srgb: false);
            AssertImport("Assets/_Project/Art/ThirdParty/texturecan/ground_0018/ground_0018_color_2k.jpg", TextureImporterType.Default, srgb: true);
        }

        static void AssertImport(string path, TextureImporterType type, bool srgb)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, $"{path} is not an imported texture");
            Assert.That(importer.textureType, Is.EqualTo(type), $"{path} type");
            Assert.That(importer.sRGBTexture, Is.EqualTo(srgb), $"{path} sRGB");
        }
    }
}
