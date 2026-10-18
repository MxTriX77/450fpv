using System.Linq;
using Fpv450.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The K01 prefab as the catalog importer builds it (setup-asset-pipeline spec, "K01 prefab is complete").
    /// Needs a prior <c>pwsh tools/build_asset.ps1 K01</c>.
    /// </summary>
    public sealed class CatalogImporterTests
    {
        const string PrefabPath = "Assets/_Project/Prefabs/debris/K01_whole.prefab";
        const string DiffusePath = "Assets/_Project/Art/ThirdParty/polyhaven/red_brick/red_brick_diff_1k.png";
        const string MaskPath = "Assets/_Project/Art/Materials/brick_red_mask.png";
        const float Millimetre = 0.001f;

        static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"{PrefabPath} is missing: run pwsh tools/build_asset.ps1 K01");
            return prefab;
        }

        [Test]
        public void LodGroup_HasTheTwoLodMeshes()
        {
            LODGroup group = Prefab().GetComponent<LODGroup>();
            Assert.That(group, Is.Not.Null);
            LOD[] lods = group.GetLODs();
            Assert.That(lods.Length, Is.EqualTo(2));
            for (int i = 0; i < lods.Length; i++)
            {
                Assert.That(lods[i].renderers.Length, Is.EqualTo(1), $"LOD{i} renderers");
                Assert.That(lods[i].renderers[0].GetComponent<MeshFilter>().sharedMesh.name, Is.EqualTo($"K01_whole_LOD{i}"));
            }
            Assert.That(lods[0].screenRelativeTransitionHeight, Is.GreaterThan(lods[1].screenRelativeTransitionHeight));
            Assert.That(lods[1].screenRelativeTransitionHeight, Is.GreaterThan(0f));
        }

        [Test]
        public void BoxCollider_MatchesTheMetadataBox()
        {
            GameObject prefab = Prefab();
            Collider[] colliders = prefab.GetComponentsInChildren<Collider>();
            Assert.That(colliders.Length, Is.EqualTo(1));
            var box = colliders[0] as BoxCollider;
            Assert.That(box, Is.Not.Null, "the one collider is a BoxCollider");

            AssetPhysicsMeta.CollisionShape shape = prefab.GetComponent<AssetPhysicsMeta>().collision.Single();
            Assert.That(shape.kind, Is.EqualTo(AssetPhysicsMeta.ShapeKind.Box));
            Vector3 centre = box.transform.localPosition + box.transform.localRotation * box.center;
            AssertWithin(centre, shape.center, "collider centre vs metadata");
            AssertWithin(box.size, shape.size, "collider size vs metadata");
            AssertWithin(shape.size, new Vector3(0.250f, 0.065f, 0.120f), "the K01 brick box (AST-04)");
        }

        [Test]
        public void Material_UsesThePinnedBrickDiffuse()
        {
            Renderer lod0 = Prefab().GetComponent<LODGroup>().GetLODs()[0].renderers[0];
            Material material = lod0.sharedMaterial;
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(DiffusePath);
            Assert.That(diffuse, Is.Not.Null, $"{DiffusePath} is missing: run python tools/assets/fetch.py red_brick");
            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(diffuse));
        }

        /// <summary>p1-pipeline-hardening, "Packed mask for a material": the derived mask feeds URP/Lit's metallic-gloss and occlusion slots.</summary>
        [Test]
        public void Material_PacksTheDerivedMaskIntoMetallicGlossAndOcclusion()
        {
            Material material = Prefab().GetComponent<LODGroup>().GetLODs()[0].renderers[0].sharedMaterial;
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);
            Assert.That(mask, Is.Not.Null, $"{MaskPath} is missing: run python tools/assets/derive.py brick_red");
            Assert.That(material.GetTexture("_MetallicGlossMap"), Is.SameAs(mask));
            Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
            Assert.That(material.GetTexture("_OcclusionMap"), Is.SameAs(mask));
            Assert.That(material.IsKeywordEnabled("_OCCLUSIONMAP"), Is.True);
            Assert.That(material.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"), Is.False, "smoothness reads from the mask's alpha");
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(1f), "the mask's alpha is not scaled");
            var importer = (TextureImporter)AssetImporter.GetAtPath(MaskPath);
            Assert.That(importer.sRGBTexture, Is.False, "the mask is linear data");
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
        }

        [Test]
        public void PhysicsMeta_CarriesMasonry()
        {
            AssetPhysicsMeta meta = Prefab().GetComponent<AssetPhysicsMeta>();
            Assert.That(meta, Is.Not.Null);
            Assert.That(meta.id, Is.EqualTo("K01"));
            Assert.That(meta.schema, Is.EqualTo(1));
            Assert.That(meta.materials.Single().physics, Is.EqualTo("masonry"));
            Assert.That(meta.collision.Single().material, Is.EqualTo("masonry"));
            Assert.That(meta.sourceJson, Is.Not.Null);
        }

        static void AssertWithin(Vector3 actual, Vector3 expected, string what)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Millimetre), $"{what} x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Millimetre), $"{what} y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Millimetre), $"{what} z");
        }
    }
}
