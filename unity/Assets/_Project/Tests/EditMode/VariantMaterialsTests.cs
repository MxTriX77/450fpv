using System;
using System.Collections.Generic;
using System.Linq;
using Fpv450.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// Per-variant material overrides (asset-pipeline "Per-variant material overrides", scenario "Autumn leaf on one
    /// variant"; p1-v01-locust design decision 7): the catalog importer resolves a variant's slot through the entry's
    /// <c>variant_materials</c> first, and the autumn variants' prefabs take the autumn leaf (beside the summer one in a
    /// mixed crown, task 1.5) while every summer variant keeps only the summer one. The prefab tests need a prior
    /// <c>pwsh tools/build_asset.ps1 V13</c> and <c>V01</c>.
    /// </summary>
    public sealed class VariantMaterialsTests
    {
        const string What = "test catalog V13";

        static Dictionary<string, object> Entry(string overridesJson) => Json.Obj(Json.Parse(
            "{\"class\": \"vegetation\", \"generator\": \"shrub\", \"variants\": [\"V13_mid\", \"V13_mid_autumn\"], " +
            "\"materials\": {\"stem\": \"bark_shrub\", \"leaf\": \"leaf_shrub\", \"far\": \"leaf_shrub_far\"}" +
            (overridesJson == null ? "" : $", \"variant_materials\": {overridesJson}") + "}"), What);

        [Test]
        public void Visuals_AnOverrideTakesItsSlotForItsVariantOnly()
        {
            var entry = Entry("{\"V13_mid_autumn\": {\"leaf\": \"leaf_shrub_autumn\", \"far\": \"leaf_shrub_autumn_far\"}}");
            Dictionary<string, string> autumn = CatalogImporter.Visuals(entry, "V13_mid_autumn", What);
            Dictionary<string, string> summer = CatalogImporter.Visuals(entry, "V13_mid", What);
            Assert.That(autumn["leaf"], Is.EqualTo("leaf_shrub_autumn"));
            Assert.That(autumn["far"], Is.EqualTo("leaf_shrub_autumn_far"));
            Assert.That(autumn["stem"], Is.EqualTo("bark_shrub"), "a slot the override leaves takes the asset's");
            Assert.That(summer, Is.EquivalentTo(CatalogImporter.Visuals(Entry(null), "V13_mid", What)), "every other variant keeps the asset's");
            Assert.That(summer["leaf"], Is.EqualTo("leaf_shrub"));
        }

        [TestCase("{\"V13_tall_autumn\": {\"leaf\": \"leaf_shrub_autumn\"}}", "names V13_tall_autumn, which is not one of its variants")]
        [TestCase("{\"V13_mid_autumn\": {\"pod\": \"pod_locust\"}}", "names slot \"pod\", which its materials do not map")]
        [TestCase("{\"V13_mid_autumn\": {\"leaf\": \"\"}}", "variant_materials.V13_mid_autumn.leaf must be a material name")]
        public void Visuals_RefusesAMalformedOverrideForAnyVariant(string overridesJson, string message)
        {
            // Asked for the summer variant: an override that cannot apply is refused whichever variant is imported.
            var e = Assert.Throws<FormatException>(() => CatalogImporter.Visuals(Entry(overridesJson), "V13_mid", What));
            StringAssert.Contains(message, e.Message);
        }

        // p1-v01-locust task 1.5: an autumn crown mixes both leaves, a share of its cards in the leaf_autumn slot (and
        // the shrub's far cards in far_autumn), while the yellowing clump takes the autumn leaf through its override.
        [TestCase("V13_mid_autumn", "V13_mid", "leaf_shrub_autumn", "leaf_shrub", true)]
        [TestCase("V13_low_autumn", "V13_low", "leaf_shrub_autumn", "leaf_shrub", true)]
        [TestCase("V13_tall_autumn", "V13_tall", "leaf_shrub_autumn", "leaf_shrub", true)]
        [TestCase("V13_mid_yellowing", "V13_mid", "leaf_shrub_autumn", "leaf_shrub", false)]
        [TestCase("V01_S_autumn", "V01_S", "leaf_locust_autumn", "leaf_locust", true)]
        [TestCase("V01_M_autumn", "V01_M", "leaf_locust_autumn", "leaf_locust", true)]
        [TestCase("V01_L_autumn", "V01_L", "leaf_locust_autumn", "leaf_locust", true)]
        public void AutumnPrefab_TakesTheAutumnLeafAndTheSummerOneKeepsItsOwn(string autumn, string summer, string autumnLeaf, string summerLeaf, bool mixed)
        {
            string[] autumnMaterials = MeshLodMaterials(autumn), summerMaterials = MeshLodMaterials(summer);
            Assert.That(autumnMaterials, Does.Contain(autumnLeaf));
            Assert.That(autumnMaterials.Contains(summerLeaf), Is.EqualTo(mixed), mixed ? "a mixed crown keeps the summer leaf too" : "the override takes the slot");
            Assert.That(summerMaterials, Does.Contain(summerLeaf));
            Assert.That(summerMaterials.Where(m => m.Contains("autumn")), Is.Empty);
            if (autumnLeaf == "leaf_shrub_autumn") // LOD2's far cards follow the leaves
            {
                Assert.That(autumnMaterials, Does.Contain("leaf_shrub_autumn_far"));
                Assert.That(autumnMaterials.Contains("leaf_shrub_far"), Is.EqualTo(mixed));
                Assert.That(summerMaterials, Does.Contain("leaf_shrub_far"));
            }
        }

        /// <summary>The material names on every mesh LOD of a committed vegetation prefab.</summary>
        static string[] MeshLodMaterials(string variant)
        {
            string path = $"Assets/_Project/Prefabs/vegetation/{variant}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"{path} is missing: run pwsh tools/build_asset.ps1 {variant.Substring(0, 3)}");
            return prefab.GetComponent<LODGroup>().GetLODs()
                .SelectMany(lod => lod.renderers)
                .Where(r => r.GetComponent<MeshFilter>().sharedMesh.name.Contains("_LOD"))
                .SelectMany(r => r.sharedMaterials.Select(m => m.name))
                .Distinct()
                .ToArray();
        }
    }
}
