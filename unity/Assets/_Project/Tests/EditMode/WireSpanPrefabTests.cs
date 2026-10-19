using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.Assets;
using Fpv450.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The L03 prefabs as the catalog importer builds them (p1-l01-l03-pole-wires, "Ports and wires in the metadata",
    /// "Wires are audited against their polyline" and "Catenary and clearance"): the metadata component exposes one
    /// wire per conductor, material cable, at its diameter and with its point list; the medium span's four 9 mm
    /// conductors hang from L01_straight's four ports and sag 0.8 m at mid-span; no collider stands for a wire.
    /// Needs a prior <c>pwsh tools/build_asset.ps1 L03</c>.
    /// </summary>
    public sealed class WireSpanPrefabTests
    {
        const string Medium = "Assets/_Project/Prefabs/lines/L03_medium.prefab";
        const string Thick = "Assets/_Project/Prefabs/lines/L03_thick.prefab";
        const string MediumFbx = "Assets/_Project/Art/Models/lines/L03/L03_medium.fbx";
        const string MediumMeta = "Assets/_Project/Art/Models/lines/L03/L03_medium.assetmeta.json";
        const string Aluminium = "Assets/_Project/Art/Materials/wire_aluminium.mat";
        const string Throwaway = "Assets/_Project/Tests/EditMode/ThrowawayThinWireFlag";

        static GameObject Prefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"{path} is missing: run pwsh tools/build_asset.ps1 L03");
            return prefab;
        }

        [Test]
        public void MediumSpan_ExposesFourCablesHungFromThePolePortsAndNoCollider()
        {
            GameObject prefab = Prefab(Medium);
            AssetPhysicsMeta meta = prefab.GetComponent<AssetPhysicsMeta>();
            Assert.That(meta.wires.Length, Is.EqualTo(4));
            Assert.That(meta.collision, Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty, "no collider for a wire");
            float[] ports = { 7.355f, 6.855f, 6.355f, 5.855f };
            for (int i = 0; i < 4; i++)
            {
                AssetPhysicsMeta.Wire wire = meta.wires[i];
                Assert.That(wire.material, Is.EqualTo("cable"), $"wires[{i}]");
                Assert.That(wire.slot, Is.EqualTo("aluminium"), $"wires[{i}]");
                Assert.That(wire.diameterM, Is.EqualTo(0.009f).Within(1e-6f), $"wires[{i}]");
                Assert.That(wire.massKgPerM, Is.EqualTo(0.135f).Within(1e-6f), $"wires[{i}]");
                Assert.That(wire.sagM, Is.EqualTo(0.8f).Within(1e-6f), $"wires[{i}]");
                Assert.That(wire.axialStiffnessN, Is.EqualTo(3.1185e6f).Within(1f), $"wires[{i}]: 63 GPa over A-50's 49.5 mm²");
                Assert.That(wire.breakingForceN, Is.EqualTo(8198f).Within(0.01f), $"wires[{i}]: A-50's published 8 198 N");
                Assert.That(wire.damage, Is.EqualTo("intact"), $"wires[{i}]");
                Assert.That(wire.points.Length, Is.EqualTo(81), $"wires[{i}]: 2 points a metre over 40 m");
                Assert.That(wire.points[0].y, Is.EqualTo(ports[i]).Within(1e-4f), $"wires[{i}] at the first pole");
                Assert.That(wire.points[80].x, Is.EqualTo(40f).Within(1e-4f), $"wires[{i}] at the second pole");
                Assert.That(wire.points.Min(p => p.y), Is.EqualTo(ports[i] - 0.8f).Within(0.010f), $"wires[{i}] lowest point");
            }
            Assert.That(meta.snag.Length, Is.EqualTo(4), "the whole wire a snag");
            Assert.That(prefab.GetComponent<LODGroup>().GetLODs().Length, Is.EqualTo(2));
            Assert.That(meta.cullDistanceM, Is.EqualTo(841.5f).Within(1e-3f), "where 935 D / d falls to 1 % of a pixel");
        }

        [Test]
        public void WireRenderers_DrawThinWiresWithTheirRadiusAndRadialNormalsAndCastNoShadow()
        {
            GameObject prefab = Prefab(Medium);
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.EqualTo(2), "one renderer per LOD");
            foreach (MeshRenderer renderer in renderers)
            {
                Assert.That(renderer.sharedMaterials.Select(m => m.shader.name), Is.EqualTo(new[] { "Fpv450/ThinWire" }), renderer.name);
                Assert.That(renderer.sharedMaterial.name, Is.EqualTo("wire_aluminium_thin_wire"), renderer.name);
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off), renderer.name);
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var uv2 = new System.Collections.Generic.List<Vector2>();
                mesh.GetUVs(1, uv2);
                Assert.That(uv2.Count, Is.EqualTo(mesh.vertexCount), $"{mesh.name}: a radius per vertex");
                Assert.That(uv2.All(r => Mathf.Abs(r.x - 0.0045f) < 1e-6f && r.y == 0f), Is.True, $"{mesh.name}: uv2 is (4.5 mm, 0)");
                // the tubes run along X, sloping at most 0.08 rad at the poles: a radial normal is square to them
                Assert.That(mesh.normals.All(n => Mathf.Abs(n.magnitude - 1f) < 1e-3f && Mathf.Abs(n.x) < 0.1f), Is.True, $"{mesh.name}: radial normals");
            }
        }

        /// <summary>
        /// Review S2: the metadata's <c>thin_wire</c> flag, not uv2 (also Unity's lightmap channel), picks the
        /// thin-wire material. The medium span's own FBX, whose tubes carry their radius in uv2, built from its own
        /// metadata with both LODs unflagged: without its wires every slot draws URP/Lit; with them the import fails
        /// naming the wire's slot. Everything it writes goes under <see cref="Throwaway"/>, deleted after.
        /// </summary>
        [Test]
        public void ThinWireFlag_NotTheUv2_PicksTheMaterial()
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(MediumMeta);
            Assert.That(source, Is.Not.Null, $"{MediumMeta} is missing: run pwsh tools/build_asset.ps1 L03");
            var materials = new Dictionary<string, Material> { ["aluminium"] = AssetDatabase.LoadAssetAtPath<Material>(Aluminium) };
            Assert.That(materials["aluminium"], Is.Not.Null, Aluminium);
            Dictionary<string, object> Unflagged(bool wires)
            {
                Dictionary<string, object> json = Json.Obj(Json.Parse(source.text), MediumMeta);
                foreach (object lod in Json.Arr(json, "lods", MediumMeta))
                    ((Dictionary<string, object>)lod)["thin_wire"] = false;
                if (!wires)
                    json["wires"] = new List<object>();
                return json;
            }

            if (AssetDatabase.IsValidFolder(Throwaway))
                AssetDatabase.DeleteAsset(Throwaway);
            AssetDatabase.CreateFolder(Path.GetDirectoryName(Throwaway).Replace('\\', '/'), Path.GetFileName(Throwaway));
            try
            {
                string lit = $"{Throwaway}/L03_medium_unflagged.prefab";
                CatalogImporter.EnsurePrefab(lit, "L03_medium_unflagged", Unflagged(false), source, MediumFbx, materials);
                MeshRenderer[] renderers = AssetDatabase.LoadAssetAtPath<GameObject>(lit).GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.EqualTo(2), "one renderer per LOD");
                foreach (MeshRenderer renderer in renderers)
                {
                    var uv2 = new List<Vector2>();
                    renderer.GetComponent<MeshFilter>().sharedMesh.GetUVs(1, uv2);
                    Assert.That(uv2.Count > 0 && uv2.All(r => r.x > 0f), Is.True, $"{renderer.name}: the radius is still in uv2");
                    Assert.That(renderer.sharedMaterials.Select(m => m.shader.name), Is.EqualTo(new[] { "Universal Render Pipeline/Lit" }),
                        $"{renderer.name}: unflagged, so lit whatever uv2 holds");
                }
                var error = Assert.Throws<FormatException>(() => CatalogImporter.EnsurePrefab($"{Throwaway}/L03_medium_wires.prefab",
                    "L03_medium_wires", Unflagged(true), source, MediumFbx, materials));
                Assert.That(error.Message, Does.Contain("slot aluminium: a wire's tube draws only in a LOD whose metadata entry is thin_wire"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(Throwaway);
            }
        }

        [Test]
        public void ThickSpan_IsTheFifteenMillimetreTwoBySixteenDropInTheInsulatedSlot()
        {
            GameObject prefab = Prefab(Thick);
            AssetPhysicsMeta.Wire wire = prefab.GetComponent<AssetPhysicsMeta>().wires.Single();
            Assert.That(wire.slot, Is.EqualTo("insulated"));
            Assert.That(wire.diameterM, Is.EqualTo(0.015f).Within(1e-6f), "the sheet's 2 x 16 mm² drop");
            Assert.That(wire.massKgPerM, Is.EqualTo(0.1285f).Within(1e-6f), "published 2 x 16");
            Assert.That(wire.axialStiffnessN, Is.EqualTo(2.016e6f).Within(1f), "63 GPa over two 16 mm² cores");
            Assert.That(wire.breakingForceN, Is.EqualTo(5299.7f).Within(0.1f), "A-50's 165.6 N/mm² over the cores");
            Assert.That(wire.points.Length, Is.EqualTo(41));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
    }
}
