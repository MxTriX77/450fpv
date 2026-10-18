using System.Linq;
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

        [Test]
        public void ThickSpan_IsOneTwentyMillimetreBundleInTheInsulatedSlot()
        {
            GameObject prefab = Prefab(Thick);
            AssetPhysicsMeta.Wire wire = prefab.GetComponent<AssetPhysicsMeta>().wires.Single();
            Assert.That(wire.slot, Is.EqualTo("insulated"));
            Assert.That(wire.diameterM, Is.EqualTo(0.020f).Within(1e-6f));
            Assert.That(wire.points.Length, Is.EqualTo(41));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
    }
}
