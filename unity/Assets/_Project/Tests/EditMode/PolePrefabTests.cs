using System.Linq;
using Fpv450.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The L01 prefabs as the catalog importer builds them (p1-l01-l03-pole-wires, "Ports written and read" and "Brace
    /// gap against the drone"): the metadata component exposes the four ports of the straight pole at its insulators'
    /// necks with outward unit directions and the TF-20 groove's seat, the braced end pole's brace triangle as one gap,
    /// the leaning pole none (L 1-3 show no brace), and the insulators as cylinder colliders. Needs a prior
    /// <c>pwsh tools/build_asset.ps1 L01</c>.
    /// </summary>
    public sealed class PolePrefabTests
    {
        const string Straight = "Assets/_Project/Prefabs/lines/L01_straight.prefab";
        const string Leaning = "Assets/_Project/Prefabs/lines/L01_leaning.prefab";
        const string Braced = "Assets/_Project/Prefabs/lines/L01_braced.prefab";

        static GameObject Prefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"{path} is missing: run pwsh tools/build_asset.ps1 L01");
            return prefab;
        }

        static AssetPhysicsMeta Meta(string path) => Prefab(path).GetComponent<AssetPhysicsMeta>();

        [Test]
        public void StraightPole_ExposesFourOutwardPortsAtItsInsulatorNecks()
        {
            AssetPhysicsMeta meta = Meta(Straight);
            Assert.That(meta.ports.Select(p => p.id), Is.EqualTo(new[] { "hook_0", "hook_1", "hook_2", "hook_3" }));
            float[] heights = { 7.355f, 6.855f, 6.355f, 5.855f };
            for (int i = 0; i < 4; i++)
            {
                AssetPhysicsMeta.Port port = meta.ports[i];
                Assert.That(port.position.y, Is.EqualTo(heights[i]).Within(1e-4f), port.id);
                Assert.That(port.direction, Is.EqualTo(new Vector3(0f, 0f, i % 2 == 0 ? 1f : -1f)), port.id);
                Assert.That(Vector3.Dot(port.position, port.direction), Is.GreaterThan(0.24f), $"{port.id} lies outward of the pole");
                Assert.That(port.maxDiameterM, Is.EqualTo(0.014f).Within(1e-6f), $"{port.id}: the TF-20 groove's width");
            }
            var insulators = meta.collision.Where(c => c.material == "glass").ToArray();
            Assert.That(insulators.Length, Is.EqualTo(4));
            Assert.That(insulators.All(c => c.kind == AssetPhysicsMeta.ShapeKind.Cylinder && Mathf.Approximately(c.radius, 0.0375f)), Is.True);
        }

        [Test]
        public void BracedPole_CarriesTheBraceTriangleAndNothingLandable()
        {
            AssetPhysicsMeta meta = Meta(Braced);
            AssetPhysicsMeta.Gap gap = meta.gaps.Single();
            Assert.That(gap.id, Is.EqualTo("brace_triangle_any_yaw_below_2.95m_nose_first_below_3.24m"));
            Assert.That(gap.polygon.Length, Is.EqualTo(3));
            Assert.That(gap.narrowestM, Is.EqualTo(1.6109f).Within(0.0005f));
            Assert.That(meta.collision.Any(c => c.landable), Is.False);
            Assert.That(meta.ports.Length, Is.EqualTo(4));
            Assert.That(meta.windVolumes.Single().porosity, Is.EqualTo(0f));
        }

        [Test]
        public void LeaningPole_HasNoBraceAndItsCrossarmUnderItsHooks()
        {
            AssetPhysicsMeta meta = Meta(Leaning);
            Assert.That(meta.gaps, Is.Empty, "L 1-3 show no brace");
            Assert.That(meta.ports.Length, Is.EqualTo(4));
            Assert.That(meta.ports[0].position.y, Is.GreaterThan(meta.ports[2].position.y + 1f), "the hooks above the crossarm's pins");
        }
    }
}
