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
    /// every primitive, the cut's are not landable and the parapets' and margins' are, no wind volume, no snag; the
    /// trench_wall material tiles its wall-space strip, the grassed_spoil cover spans the patch (task 1.4), and the
    /// grass draws on LOD0 and LOD1 without a collider (task 1.5). Needs a
    /// prior <c>pwsh tools/build_asset.ps1 E01</c>.
    /// </summary>
    public sealed class TrenchPrefabTests
    {
        const string PrefabPath = "Assets/_Project/Prefabs/earthworks/E01_curving.prefab";
        const string WallPath = "Assets/_Project/Art/Materials/trench_wall.mat";
        const float StripU = 27f, StripV = 4f, DetailTileM = 2.07f; // assets-src/materials/trench_wall.json
        const string GroundPath = "Assets/_Project/Art/Materials/grassed_spoil.mat";
        const float GroundMapM = 40f, GroundDetailTileM = 2f; // assets-src/materials/grassed_spoil.json

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
            Assert.That(meta.micro.Select(m => (m.kind, m.lod)), Is.EqualTo(new[] { ("straw", 0), ("grass", 0), ("grass", 1) }));
        }

        /// <summary>Task 1.5: the lips' and parapets' grass draws on LOD0 and LOD1 (the 1.5 m and 10 m views lie past
        /// LOD0's 8 m from the patch's centre) and not on LOD2, and no collider stands for it.</summary>
        [Test]
        public void Grass_DrawsOnLod0AndLod1_WithoutColliders()
        {
            GameObject prefab = Prefab();
            LOD[] lods = prefab.GetComponent<LODGroup>().GetLODs();
            Assert.That(lods[0].renderers.Select(r => r.name), Does.Contain("E01_curving_LOD0_grass"));
            Assert.That(lods[1].renderers.Select(r => r.name), Does.Contain("E01_curving_LOD1_grass"));
            Assert.That(lods[2].renderers.Any(r => r.name.Contains("grass")), Is.False);
            foreach (Renderer grass in lods[0].renderers.Concat(lods[1].renderers).Where(r => r.name.Contains("grass")))
                Assert.That(grass.GetComponentsInChildren<Collider>(true), Is.Empty, grass.name);
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

        /// <summary>Task 1.4: the parapets, berm and margins take the grassed-spoil cover (no bare patch or strip, so
        /// no stain without relief under it), its one map centred on the patch, which the generator UVs from its
        /// centre; S04's belt_straw is not on the trench at all.</summary>
        [Test]
        public void GroundMaterial_IsTheGrassedSpoilCoverLaidOnceOverThePatch()
        {
            GameObject prefab = Prefab();
            var ground = AssetDatabase.LoadAssetAtPath<Material>(GroundPath);
            Assert.That(ground, Is.Not.Null, $"{GroundPath} is missing");
            Assert.That(ground.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(1f / GroundMapM, 1f / GroundMapM)));
            Assert.That(ground.GetTextureOffset("_BaseMap"), Is.EqualTo(new Vector2(0.5f, 0.5f)), "centred on the UVs' origin");
            Assert.That(ground.GetTextureScale("_DetailAlbedoMap"), Is.EqualTo(Vector2.one * (GroundMapM / GroundDetailTileM)));
            Assert.That(ground.GetTexture("_BaseMap").name, Is.EqualTo("grassed_spoil_albedo"));
            string[] used = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(m => m != null).Select(m => m.name).Distinct().ToArray();
            Assert.That(used, Does.Contain("grassed_spoil"));
            Assert.That(used, Does.Not.Contain("belt_straw"));
        }
    }
}
