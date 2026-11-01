using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.Sim;
using Fpv450.Sim.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The sim sees what the scene shows (p6-world-query-gallery 1.4, world-query spec): every collider of every placed
    /// prefab in the committed test range scene lies within 1 mm and 0.01° of the shape the sim's MapWorld loads from the
    /// same placement file and metadata, with the same size, and the sim has no shape the scene lacks. Needs a prior
    /// <c>-executeMethod Fpv450.TestRangeScene.Build</c>, as <see cref="TestRangeTests"/>.
    /// </summary>
    public sealed class TestRangeWorldTests
    {
        const float ToleranceM = 0.001f, ToleranceDeg = 0.01f;

        static Vector3 V(Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

        /// <summary>The angle between a scene axis and a sim axis, in double (Vector3.Angle's float acos resolves only 0.02°).</summary>
        static float AngleDeg(Vector3 scene, Double3 sim)
        {
            var a = new Double3(scene.x, scene.y, scene.z);
            return (float)(System.Math.Atan2(Double3.Cross(a, sim).Length(), Double3.Dot(a, sim)) * 180 / System.Math.PI);
        }

        [Test]
        public void EveryColliderOfTheSceneLiesWithinAMillimetreOfTheSimsShape()
        {
            MapWorld world = MapWorld.Load(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..")), "test-range");
            Scene scene = EditorSceneManager.OpenScene(TestRangeScene.ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform placed = scene.GetRootGameObjects().Single(r => r.name == "Placed").transform;
                var byId = new Dictionary<int, Transform>();
                foreach (Transform group in placed)
                    foreach (Transform instance in group)
                        byId.Add(int.Parse(instance.name.Substring(0, 4)), instance);
                Assert.That(byId.Count, Is.EqualTo(world.PlacedObjects), "one scene instance per sim object");

                int colliders = 0;
                float worstM = 0f, worstDeg = 0f;
                for (int id = 0; id < world.PlacedObjects; id++)
                {
                    Transform t = byId[id];
                    Assert.That(t.name, Is.EqualTo(TestRangeScene.InstanceName(id, world.ObjectName(id))), $"object {id}");
                    for (int s = 0; world.Geometry(id, s, out ShapeGeometry g); s++)
                    {
                        string key = $"{t.name} shape {s}";
                        if (g.Kind == ShapeKind.Wire)
                        {
                            Assert.That(t.Find($"Collision{s}"), Is.Null, $"{key}: a wire has no collider");
                            continue;
                        }
                        Transform host = t.Find($"Collision{s}");
                        Assert.That(host, Is.Not.Null, $"{key}: no collider in the scene");
                        Collider c = host.GetComponent<Collider>();
                        Assert.That(c, Is.Not.Null, key);
                        float off = Vector3.Distance(host.position, V(g.Center));
                        float turn = AngleDeg(host.up, g.AxisY);
                        if (g.Kind == ShapeKind.Box)
                        {
                            turn = Mathf.Max(turn, AngleDeg(host.right, g.AxisX));
                            Vector3 size = Vector3.Scale(((BoxCollider)c).size, host.lossyScale);
                            Assert.That(Vector3.Distance(size, 2f * V(g.HalfExtents)), Is.LessThanOrEqualTo(ToleranceM), $"{key}: size");
                        }
                        else if (g.Kind == ShapeKind.Sphere)
                            Assert.That(((SphereCollider)c).radius, Is.EqualTo((float)g.Radius).Within(ToleranceM), $"{key}: radius");
                        else if (g.Kind == ShapeKind.Capsule)
                        {
                            var capsule = (CapsuleCollider)c;
                            Assert.That(capsule.radius, Is.EqualTo((float)g.Radius).Within(ToleranceM), $"{key}: radius");
                            Assert.That(capsule.height / 2f - capsule.radius, Is.EqualTo((float)g.HalfLength).Within(ToleranceM), $"{key}: core");
                        }
                        else
                            Assert.That(c, Is.InstanceOf<MeshCollider>(), $"{key}: a cylinder's noclip collider");
                        Assert.That(off, Is.LessThanOrEqualTo(ToleranceM), $"{key}: position");
                        Assert.That(turn, Is.LessThanOrEqualTo(ToleranceDeg), $"{key}: axes");
                        worstM = Mathf.Max(worstM, off);
                        worstDeg = Mathf.Max(worstDeg, turn);
                        colliders++;
                    }
                    int count = 0;
                    while (world.Geometry(id, count, out _))
                        count++;
                    Assert.That(t.Find($"Collision{count}"), Is.Null, $"{t.name}: a collider the sim lacks");
                }
                TestContext.Out.WriteLine($"{world.PlacedObjects} objects, {colliders} colliders: worst position {1000f * worstM:0.000} mm, worst turn {worstDeg:0.0000}°");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
