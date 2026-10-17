using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The X-sign probe (setup-asset-pipeline design decision 5, physics review R1): an asymmetric box spanning
    /// x 0..0.3, y 0..0.1, z 0..0.2 in asset space, exported by <c>tools/blender/tests/make_xsign_probe.py</c>
    /// through the same path as every asset. Unity must import it with those features on its own +X, +Y and +Z
    /// and wound outward; a wrong sign in <c>lib/frame.py</c> fails here before it mirrors a house's door.
    /// </summary>
    public sealed class XSignProbeTests
    {
        const string FixturePath = "Assets/_Project/Tests/EditMode/Fixtures/xsign_probe.fbx";
        const float Millimetre = 0.001f;
        static readonly Vector3 Size = new Vector3(0.3f, 0.1f, 0.2f);

        static Mesh Probe()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(FixturePath);
            Assert.That(mesh, Is.Not.Null, $"{FixturePath} is missing: run tools/blender/tests/make_xsign_probe.py in Blender");
            return mesh;
        }

        [Test]
        public void ImportedBounds_RunFromTheOriginToThePositiveSize()
        {
            Bounds bounds = Probe().bounds;
            AssertWithin(bounds.min, Vector3.zero, "bounds min");
            AssertWithin(bounds.max, Size, "bounds max");
        }

        [Test]
        public void Triangles_WindOutward()
        {
            Mesh mesh = Probe();
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Assert.That(triangles.Length, Is.EqualTo(12 * 3));
            Vector3 centre = Size / 2f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a); // a front face is clockwise seen from outside
                Vector3 outward = (a + b + c) / 3f - centre;
                Assert.That(Vector3.Dot(normal, outward), Is.GreaterThan(0f), $"triangle {i / 3} faces inward");
            }
        }

        static void AssertWithin(Vector3 actual, Vector3 expected, string what)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Millimetre), $"{what} x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Millimetre), $"{what} y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Millimetre), $"{what} z");
        }
    }
}
