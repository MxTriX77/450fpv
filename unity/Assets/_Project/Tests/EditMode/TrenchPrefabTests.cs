using System.Linq;
using Fpv450.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The E01 prefab as the catalog importer builds it (p1-e01-trench, "A trench writes its hole" and "Parapets and
    /// clods are ground"): the metadata component exposes the run's hole first and one per niche, a collider stands for
    /// every primitive, the cut's are not landable and the parapets' and margins' are, no wind volume, no snag; and the
    /// trench_wall material tiles its wall-space strip. Needs a prior <c>pwsh tools/build_asset.ps1 E01</c>.
    /// </summary>
    public sealed class TrenchPrefabTests
    {
        const string PrefabPath = "Assets/_Project/Prefabs/earthworks/E01_curving.prefab";
        const string WallPath = "Assets/_Project/Art/Materials/trench_wall.mat";
        const float StripU = 27f, StripV = 4f, DetailTileM = 2.07f; // assets-src/materials/trench_wall.json

        static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"{PrefabPath} is missing: run pwsh tools/build_asset.ps1 E01");
            return prefab;
        }

        [Test]
        public void Meta_ExposesTheRunsHoleThenTheNiches()
        {
            AssetPhysicsMeta meta = Prefab().GetComponent<AssetPhysicsMeta>();
            Assert.That(meta.holes.Select(h => h.id), Is.EqualTo(new[] { "run", "niche_0", "niche_1" }));
            AssetPhysicsMeta.Hole run = meta.holes[0];
            Assert.That(run.centreLine.Length, Is.GreaterThanOrEqualTo(25), "a point per metre at least");
            Assert.That(run.centreLine.All(p => p.y == 0f), Is.True, "[x, 0, z] in asset space");
            Assert.That(run.centreLine[0], Is.EqualTo(Vector3.zero));
            Assert.That(run.topWidthM, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(run.depthM, Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(run.bandWidthM, Is.EqualTo(2.3f).Within(1e-5f));
            foreach (AssetPhysicsMeta.Hole niche in meta.holes.Skip(1))
            {
                Assert.That(niche.centreLine.Length, Is.EqualTo(2));
                Assert.That(niche.topWidthM, Is.EqualTo(0.8f).Within(1e-5f));
                Assert.That(niche.depthM, Is.EqualTo(1.35f).Within(1e-5f));
                Assert.That(niche.bandWidthM, Is.EqualTo(2.1f).Within(1e-5f));
            }
        }

        [Test]
        public void Colliders_StandForEveryPrimitive_CutNotLandable_GroundLandable()
        {
            GameObject prefab = Prefab();
            AssetPhysicsMeta meta = prefab.GetComponent<AssetPhysicsMeta>();
            Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(meta.collision.Length));
            Assert.That(meta.collision.Where(c => c.material == "surface:belt_bare").All(c => !c.landable), Is.True);
            Assert.That(meta.collision.Where(c => c.material == "surface:belt_straw").All(c => c.landable), Is.True);
            Assert.That(meta.collision.Count(c => c.material == "surface:crater_spoil"), Is.GreaterThan(0));
            Assert.That(meta.windVolumes, Is.Empty);
            Assert.That(meta.snag, Is.Empty);
            Assert.That(meta.micro.Select(m => m.kind), Is.EqualTo(new[] { "straw" }));
        }

        [Test]
        public void WallMaterial_TilesItsStrip()
        {
            Prefab();
            var wall = AssetDatabase.LoadAssetAtPath<Material>(WallPath);
            Assert.That(wall, Is.Not.Null, $"{WallPath} is missing");
            Assert.That(wall.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(1f / StripU, 1f / StripV)));
            Assert.That(wall.GetTextureOffset("_BaseMap"), Is.EqualTo(Vector2.zero), "the strip starts at its corner");
            Assert.That(wall.GetTextureScale("_DetailAlbedoMap"), Is.EqualTo(new Vector2(StripU / DetailTileM, StripV / DetailTileM)));
            Assert.That(wall.IsKeywordEnabled("_DETAIL_MULX2"), Is.True);
            Assert.That(wall.GetTexture("_BaseMap").name, Is.EqualTo("trench_wall_albedo"));
        }
    }
}
