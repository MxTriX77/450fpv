using System;
using System.Collections.Generic;
using Fpv450.Fiber;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fpv450.Tests.Fiber
{
    /// <summary>
    /// The coil's pack radius following payout (p7d-tether 1.14, DRN-05): D01's pack from drone.json and the look's coil
    /// stations, the generator's law (drone.py's coil_radii), and the pack moved on a model whose spool is drone.py's LOD0
    /// lathe (the rear cap, the ribbed pack, the front hoop) with a cable tie's head on the cap, under a turned and offset
    /// child, beside a pack box that must not move.
    /// </summary>
    public sealed class CoilPackTests
    {
        const float Rib = CoilShape.D01RibDepthM;

        [Test]
        public void D01sPackFollowsTheGeneratorsLaw()
        {
            CoilShape s = CoilShape.D01(RepositoryDrone.Spec());
            Assert.That(s.Centre, Is.EqualTo(new Vector3(0f, -0.053f, 0f)), "drone.json's coil beneath the frame");
            Assert.That(s.Axis, Is.EqualTo(Vector3.forward), "fore-aft, the line leaving aft");
            Assert.That(s.Radius, Is.EqualTo(0.0415f).Within(1e-7f));
            Assert.That(s.PackRear, Is.EqualTo(-0.065f).Within(1e-7f));
            Assert.That(s.PackFront, Is.EqualTo(0.085f).Within(1e-7f));
            Assert.That(s.FiberLength, Is.EqualTo(10000));
            Assert.That(s.PackRadius(10000), Is.EqualTo(0.0415).Within(1e-7), "full");
            Assert.That(s.PackRadius(0), Is.EqualTo(0.0195).Within(1e-7), "empty: the core");
            Assert.That(s.PackRadius(5000), Is.EqualTo(Math.Sqrt((0.0415 * 0.0415 + 0.0195 * 0.0195) / 2)).Within(1e-7), "half the line, half the pack's volume");
            Assert.That(s.PackRadius(9990), Is.LessThan(s.PackRadius(10000)));
            // The pack the look draws holds the line the physics flies: 10 km of 0.25 mm line at a square lay.
            double d = TetherScenarios.Table().Number("fiber.coating_diameter_m");
            double drawn = Math.PI * (s.Radius * (double)s.Radius - s.CoreRadius * (double)s.CoreRadius) * CoilShape.D01WoundM;
            double line = s.FiberLength * d * d;
            TestContext.WriteLine($"the pack's volume {drawn:E4} m³, the line's at a square lay {line:E4} m³ ({drawn / line - 1:+0.0%;-0.0%}); " +
                                  $"radius {s.PackRadius(10000) * 1000:F2} mm full, {s.PackRadius(5000) * 1000:F2} mm at half, {s.PackRadius(0) * 1000:F2} mm empty; " +
                                  $"{(s.PackRadius(10000) - s.PackRadius(9990)) * 1e6:F1} µm less for the first 10 m");
            Assert.That(drawn, Is.EqualTo(line).Within(0.02 * line));
        }

        [Test]
        public void ThePackShrinksOnTheModelAndNothingElseMoves()
        {
            CoilShape shape = CoilShape.D01(RepositoryDrone.Spec());
            GameObject model = Model(shape, out MeshFilter coil, out MeshFilter box);
            try
            {
                Mesh original = coil.sharedMesh, boxMesh = box.sharedMesh;
                Vector3[] before = original.vertices;
                var pack = new CoilPack(model, shape);
                Assert.That(pack.Unreadable, Is.EqualTo(0));
                Assert.That(coil.sharedMesh, Is.Not.SameAs(original), "the coil's mesh cloned for this model");
                Assert.That(box.sharedMesh, Is.SameAs(boxMesh), "a mesh without the pack is left alone");
                pack.Show(2500);
                double radius = shape.PackRadius(2500), by = radius - shape.Radius;
                Vector3[] after = coil.sharedMesh.vertices;
                int moved = 0;
                for (int i = 0; i < before.Length; i++)
                {
                    // The lathe's own axes: z the coil's axis through the origin.
                    float r0 = new Vector2(before[i].x, before[i].y).magnitude, z = before[i].z;
                    bool onPack = z >= shape.PackRear - 1e-4f && z <= shape.PackFront + 1e-4f && r0 >= shape.Radius - Rib - 1e-4f && r0 <= shape.Radius + 1e-4f;
                    if (onPack)
                    {
                        moved++;
                        Assert.That(new Vector2(after[i].x, after[i].y).magnitude, Is.EqualTo(r0 + by).Within(2e-6), "vertex " + i + " on the pack");
                        Assert.That(after[i].z, Is.EqualTo(z).Within(1e-6f), "vertex " + i + " stays at its station");
                    }
                    else Assert.That(after[i], Is.EqualTo(before[i]), "vertex " + i + " off the pack stays");
                }
                TestContext.WriteLine($"{moved} of {before.Length} vertices on the pack, moved {by * 1000:F3} mm to {radius * 1000:F3} mm at 2.5 km left");
                Assert.That(moved, Is.EqualTo(pack.Vertices));
                Assert.That(moved, Is.EqualTo(Ring * (2 + 4 * Ribs - 1)), "the rear face's rim, the ribbed pack's 79 grooves and the hoop's inner rim, no more");
                pack.Dispose();
                Assert.That(coil.sharedMesh, Is.SameAs(original), "the model gets its own mesh back");
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        // ---- the model ----

        const int Ring = 32, Ribs = 20; // drone.py's LOD0: 32 segments, 0.15 m / 7.5 mm ribs

        /// <summary>
        /// A drone model whose spool is drone.py's LOD0 lathe (the rear cap Ø72 mm, the ribbed pack at the spec's diameter and
        /// the look's 0.6 mm ribs, the front hoop Ø90 mm) on a child at the coil's centre turned 30° about its axis, with a
        /// cable tie's head box on the cap (its corners near the pack's radius), and a pack box beside it; the model itself
        /// placed and turned in the world.
        /// </summary>
        internal static GameObject Model(in CoilShape shape, out MeshFilter coil, out MeshFilter box)
        {
            var model = new GameObject("D01 (test)");
            model.transform.SetPositionAndRotation(new Vector3(120f, 4f, -30f), Quaternion.Euler(0f, 40f, 0f));
            var spool = new GameObject("coil");
            spool.transform.SetParent(model.transform, false);
            spool.transform.localPosition = shape.Centre;
            spool.transform.localRotation = Quaternion.AngleAxis(30f, Vector3.forward);
            coil = spool.AddComponent<MeshFilter>();
            coil.sharedMesh = Spool(shape);
            spool.AddComponent<MeshRenderer>();
            var pack = new GameObject("pack_left");
            pack.transform.SetParent(model.transform, false);
            pack.transform.localPosition = new Vector3(-0.0735f, -0.043f, 0f);
            box = pack.AddComponent<MeshFilter>();
            box.sharedMesh = Box(new Vector3(0.047f, 0.045f, 0.22f), Vector3.zero);
            pack.AddComponent<MeshRenderer>();
            return model;
        }

        static Mesh Spool(in CoilShape s)
        {
            float z0 = -0.1f, z3 = 0.1f, z1 = s.PackRear, z2 = s.PackFront, cap = 0.036f, hoop = 0.045f, rp = s.Radius;
            var profile = new List<Vector2> { new Vector2(z0, 0f), new Vector2(z0, cap), new Vector2(z1, cap), new Vector2(z1, rp) };
            for (int k = 1; k < Ribs * 4; k++)
            {
                float groove = 0.5f - 0.5f * Mathf.Cos(2 * Mathf.PI * k / 4);
                profile.Add(new Vector2(z1 + (z2 - z1) * k / (Ribs * 4), rp - Rib * groove));
            }
            profile.AddRange(new[] { new Vector2(z2, rp), new Vector2(z2, hoop), new Vector2(z3, hoop), new Vector2(z3, 0f) });
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (Vector2 p in profile)
                for (int a = 0; a < Ring; a++)
                {
                    float t = 2 * Mathf.PI * a / Ring;
                    vertices.Add(new Vector3(p.y * Mathf.Cos(t), p.y * Mathf.Sin(t), p.x));
                }
            for (int i = 0; i + 1 < profile.Count; i++)
                for (int a = 0; a < Ring; a++)
                {
                    int p0 = i * Ring + a, p1 = i * Ring + (a + 1) % Ring;
                    triangles.AddRange(new[] { p0, p1 + Ring, p0 + Ring, p0, p1, p1 + Ring });
                }
            // A cable tie's head on the rear cap (drone.py's tie_head): 3 mm off the cap's radius at its middle station, its
            // outer corners within the pack's radius window (41.35 mm), outside the pack's stations.
            Mesh head = Box(new Vector3(0.007f, 0.0044f, 0.006f), new Vector3(0f, cap + 0.003f, (z0 + z1) / 2));
            int offset = vertices.Count;
            vertices.AddRange(head.vertices);
            foreach (int t in head.triangles) triangles.Add(offset + t);
            Object.DestroyImmediate(head);
            var mesh = new Mesh { name = "D01 coil (test)" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        static Mesh Box(Vector3 size, Vector3 centre)
        {
            var v = new Vector3[8];
            for (int i = 0; i < 8; i++)
                v[i] = centre + Vector3.Scale(size / 2, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var mesh = new Mesh { name = "box (test)", vertices = v };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5 };
            return mesh;
        }
    }
}
