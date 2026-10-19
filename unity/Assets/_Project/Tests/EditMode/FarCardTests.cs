using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// V13's far cards as the catalog importer builds them (p1-v01-locust task 1.2j): LOD2 is three crossed cards,
    /// each two faces back to back on the one-sided far material, and every corner keeps the normal the generator
    /// gave it through the FBX: its face's side, leaning up by the leaves' visible lean (11.4–11.9° for the three
    /// variants), not its plane's horizontal normal.
    /// </summary>
    public sealed class FarCardTests
    {
        [TestCase("V13_low")]
        [TestCase("V13_mid")]
        [TestCase("V13_tall")]
        public void FarCards_KeepTheirOwnNormalsOnTheOneSidedMaterial(string variant)
        {
            string path = $"Assets/_Project/Prefabs/vegetation/{variant}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"{path} is missing: run pwsh tools/build_asset.ps1 V13");
            Renderer far = prefab.GetComponent<LODGroup>().GetLODs()[2].renderers.Single();
            Assert.That(far.sharedMaterial.name, Is.EqualTo("leaf_shrub_far"));
            Assert.That(far.sharedMaterial.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Back));

            Mesh mesh = far.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            int[] triangles = mesh.triangles;
            Assert.That(triangles.Length / 3, Is.EqualTo(60), "3 cards x 2 faces x 10 triangles");
            float lo = Mathf.Sin(11f * Mathf.Deg2Rad), hi = Mathf.Sin(12.5f * Mathf.Deg2Rad);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // Unity's front face winds clockwise, so this cross product points out of the front.
                Vector3 face = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                                             vertices[triangles[i + 2]] - vertices[triangles[i]]).normalized;
                Assert.That(Mathf.Abs(face.y), Is.LessThan(1e-4f), "the cards stand vertical");
                for (int k = 0; k < 3; k++)
                {
                    Vector3 n = normals[triangles[i + k]];
                    Assert.That(n.y, Is.InRange(lo, hi), $"triangle {i / 3}: the normal leans up 11-12.5 degrees");
                    Assert.That(Vector3.Dot(new Vector3(n.x, 0f, n.z).normalized, face), Is.GreaterThan(0.9999f),
                        $"triangle {i / 3}: it leans from its own face's side");
                }
            }
        }
    }
}
