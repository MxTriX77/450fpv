using System.Linq;
using Fpv450.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The S04 prefab as the catalog importer builds it (p1-s04-belt-straw, "Micro meshes carry no collision" and
    /// "Micro meshes skipped by the audit"): LOD0 renders the slab and the baked straws, no collider stands for the
    /// straws, the metadata component lists them under <c>micro</c>. Needs a prior <c>pwsh tools/build_asset.ps1 S04</c>.
    /// </summary>
    public sealed class GroundPatchPrefabTests
    {
        const string PrefabPath = "Assets/_Project/Prefabs/surfaces/S04_patch.prefab";
        const string MicroMesh = "S04_patch_LOD0_straw";

        static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"{PrefabPath} is missing: run pwsh tools/build_asset.ps1 S04");
            return prefab;
        }

        [Test]
        public void Lod0_RendersTheSlabAndTheMicroMesh_AndTheOtherLodsOnlyTheSlab()
        {
            LOD[] lods = Prefab().GetComponent<LODGroup>().GetLODs();
            Assert.That(lods.Length, Is.EqualTo(3));
            string[] lod0 = lods[0].renderers.Select(r => r.GetComponent<MeshFilter>().sharedMesh.name).ToArray();
            Assert.That(lod0, Is.EquivalentTo(new[] { "S04_patch_LOD0", MicroMesh }));
            Assert.That(lods[1].renderers.Select(r => r.GetComponent<MeshFilter>().sharedMesh.name), Is.EqualTo(new[] { "S04_patch_LOD1" }));
            Assert.That(lods[2].renderers.Select(r => r.GetComponent<MeshFilter>().sharedMesh.name), Is.EqualTo(new[] { "S04_patch_LOD2" }));
            Renderer straws = lods[0].renderers.Single(r => r.GetComponent<MeshFilter>().sharedMesh.name == MicroMesh);
            Assert.That(straws.sharedMaterial.name, Is.EqualTo("straw"));
            Assert.That(straws.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, Is.GreaterThan(100000), "the baked straws are geometry");
        }

        [Test]
        public void MicroMesh_HasNoCollider_AndTheMetaListsIt()
        {
            GameObject prefab = Prefab();
            Transform straws = prefab.transform.Find(MicroMesh);
            Assert.That(straws, Is.Not.Null);
            Assert.That(straws.GetComponentsInChildren<Collider>(true), Is.Empty, "a micro mesh carries no collision");
            AssetPhysicsMeta meta = prefab.GetComponent<AssetPhysicsMeta>();
            Assert.That(meta.micro.Length, Is.EqualTo(1));
            Assert.That(meta.micro[0].name, Is.EqualTo(MicroMesh));
            Assert.That(meta.micro[0].kind, Is.EqualTo("straw"));
            Assert.That(meta.micro[0].lod, Is.EqualTo(0));
            Assert.That(meta.micro[0].count, Is.GreaterThan(10000));
            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            Assert.That(colliders.Length, Is.EqualTo(meta.collision.Length), "one collider per primitive, none for the straws");
            Assert.That(colliders.OfType<BoxCollider>().Count(), Is.EqualTo(1));
            Assert.That(colliders.OfType<SphereCollider>().Count(), Is.EqualTo(meta.collision.Length - 1));
            Assert.That(meta.collision.All(c => c.landable), Is.True);
            Assert.That(meta.collision[0].material, Is.EqualTo("surface:belt_straw"));
            Assert.That(meta.collision.Skip(1).All(c => c.material == "surface:crater_spoil"), Is.True);
        }
    }
}
