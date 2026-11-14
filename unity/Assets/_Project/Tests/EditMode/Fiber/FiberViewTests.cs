using System;
using Fpv450.Fiber;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Fpv450.Tests.Fiber
{
    /// <summary>
    /// The fiber's look (p7d-tether 1.14, scenario "The line in the chase view": the drawn line passes within 1 mm of the
    /// physics' nodes and trail points published for that frame): FiberView drawn at 50 frames a second from the poses of a
    /// flight on the line (TetherScenarios: the repository's drone legs-on on the floor, its line laid behind it) through a
    /// 20 m/s cruise and a reverse; the trail's chunks rebuilt only where the trail changed; 0 B a Draw; the coil's pack at the
    /// physics' rebuild marks; the line's material.
    /// </summary>
    public sealed class FiberViewTests
    {
        const int Sides = FiberTube.Sides;

        internal static object sink; // the recorder's known-wrong's one object

        [Test]
        public void TheLineGoesThroughTheNodesAndTheTrailInACruiseAndAReverse()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            // The line laid behind in a zigzag, 60 m of it from 50 m back (TetherScenarios.PullTrail's): it freezes while the
            // drone sits on it 2 s, then lifts as the drone climbs to 5 m by 2 m/s and cruises along +z at 20 m/s to 11 s,
            // then reverses at 10 m/s to 15 s; a frame each 20 ms.
            var layout = new Double3[6];
            for (int i = 0; i < 6; i++) layout[i] = new Double3(i % 2 == 0 ? 0 : 6, 0, -50 + 10 * i);
            layout[5] = new Double3(0, 0, -0.1);
            FlightSetup setup = TetherScenarios.Setup(spec, layout: layout);
            double stand = setup.StartPosition.Y;
            TetherScenarios.Plan plan = (t, c, pilot) =>
            {
                pilot.HoldVelocity = t >= 4.5;
                if (t < 2) pilot.Target = new Double3(0, stand - 0.05, 0);
                else if (t < 4.5) pilot.Target = new Double3(0, stand + Math.Min(5, 2 * (t - 2)), 0);
                else
                {
                    pilot.TargetVelocity = new Double3(0, 0, t < 11 ? 20 : -10);
                    pilot.Target = new Double3(0, stand + 5, c.State.Position.Z);
                }
            };
            CoilShape shape = CoilShape.D01(spec);
            GameObject model = CoilPackTests.Model(shape, out _, out _);
            var go = new GameObject("Fiber (test)");
            var view = go.AddComponent<FiberView>();
            try
            {
                FlightCore core = null;
                // 0 B a Draw, read by the test framework's GC.Alloc recorder: on Unity's Mono the per-thread counter misses small
                // allocations (the sim's probe reads 0 B for a 24 B object there, AllocProbeTests' known-wrong). The recorder
                // itself is seen to see one object first.
                var allocates = new UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint();
                Assert.That(() => { sink = new object(); }, allocates, "the recorder sees one object");
                int allocated = 0;
                int frames = 0, checks = 0, mostNodes = 0, mostTrail = 0, leastTrail = int.MaxValue, coilSteps = 0;
                double worstSpan = 0, worstTrail = 0, worstCoil = 0, shown = double.NaN;
                TetherScenarios.Fly(setup, 15, plan, c =>
                {
                    if (core == null)
                    {
                        core = c;
                        view.Begin(c.Tether, setup.Tether, model, shape);
                        Assert.That(view.Coil, Is.Not.Null, "the model's coil found");
                    }
                    if (c.State.Step % 20 != 0) return;
                    c.GetPose(out PoseFrame pose);
                    PoseFrame drawn = pose;
                    if (allocates.ApplyTo(new TestDelegate(() => view.Draw(drawn))).IsSuccess) allocated++;
                    frames++;
                    mostNodes = Math.Max(mostNodes, pose.FiberNodeCount);
                    mostTrail = Math.Max(mostTrail, pose.FiberTrailCount);
                    if (pose.TimeS > 2) leastTrail = Math.Min(leastTrail, pose.FiberTrailCount);
                    double paid = Math.Floor(pose.FiberPaidOut / 10) * 10;
                    worstCoil = Math.Max(worstCoil, Math.Abs(view.Coil.ShownRadius - shape.PackRadius(c.Tether.CoilLineAtStart - paid)));
                    if (view.Coil.ShownRadius != shown) coilSteps++;
                    shown = view.Coil.ShownRadius;
                    if (frames % 10 != 0) return;
                    worstSpan = Math.Max(worstSpan, SpanError(view, pose));
                    worstTrail = Math.Max(worstTrail, TrailError(view, c.Tether.Trail, pose.FiberTrailCount));
                    checks++;
                });
                TestContext.WriteLine($"{frames} frames, {checks} checked: up to {mostNodes} nodes; the trail {mostTrail} points frozen, lifted to {leastTrail}; paid out {core.Tether.PaidOut:F1} m, " +
                                      $"{view.ChunkBuilds} chunk builds; the drawn line off the nodes by {worstSpan * 1000:F4} mm at most, off the trail by " +
                                      $"{worstTrail * 1000:F4} mm; the pack {shape.PackRadius(core.Tether.CoilLineAtStart) * 1000:F4} to {view.Coil.ShownRadius * 1000:F4} mm " +
                                      $"in {coilSteps} steps, off the law by {worstCoil:E1} m; {allocated} Draws allocating");
                Assert.That(mostNodes, Is.GreaterThan(8), "the span was drawn");
                Assert.That(mostTrail, Is.GreaterThan(4), "the trail froze and was drawn");
                Assert.That(leastTrail, Is.LessThan(mostTrail), "and lifted as the line was pulled");
                Assert.That(worstSpan, Is.LessThan(1e-3), "within 1 mm of the nodes");
                Assert.That(worstTrail, Is.LessThan(1e-3), "within 1 mm of the trail's points");
                Assert.That(worstCoil, Is.LessThan(1e-9), "the pack at the line the coil's mass carries (each 10 m)");
                Assert.That(coilSteps, Is.GreaterThan(1), "the pack shrank as the line paid out");
                Assert.That(allocated, Is.EqualTo(0), "0 B a Draw: no Draw allocates");
                Assert.That(view.SpanRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(view.SpanRenderer.sharedMaterial, Is.Not.Null.And.EqualTo(Resources.Load<Material>(FiberView.MaterialResource)));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void TheTrailIsRebuiltOnlyWhereItChanged()
        {
            var go = new GameObject("Fiber (test)");
            var view = go.AddComponent<FiberView>();
            try
            {
                const int capacity = 2000;
                view.Prepare(0.000125f, capacity);
                Assert.That(view.ChunkCount, Is.EqualTo(4));
                var trail = new Double3[capacity];
                for (int i = 0; i < capacity; i++) trail[i] = Zigzag(i, 0);

                Expect(view, trail, 1, 0, "one point: nothing to draw");
                Expect(view, trail, 600, 2, "two chunks");
                Expect(view, trail, 600, 2, "nothing changed");
                Expect(view, trail, 601, 3, "a point frozen: the last chunk");
                Expect(view, trail, 1100, 5, "the second chunk's end and a third chunk");
                Expect(view, trail, 1025, 5, "lifted back to the second chunk's last point: the third goes, the second is whole");
                trail[1024] = Zigzag(1024, 1);
                Expect(view, trail, 1025, 6, "a lift and a freeze between two frames: the second chunk again");
                for (int i = 1; i < 1025; i++) trail[i] = Zigzag(i + 1, 1); // an overflow's merge shifts every point but the anchor
                Expect(view, trail, 1025, 8, "every point moved: every chunk");
                Expect(view, trail, 1025, 8, "and then rests");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheLinesMaterialIsTheThinWireThroughTheFeed()
        {
            var material = Resources.Load<Material>(FiberView.MaterialResource);
            Assert.That(material, Is.Not.Null, "Settings/Fiber/Resources/FiberLine.mat");
            Assert.That(material.shader.name, Is.EqualTo("Fpv450/ThinWire"));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, "the shader compiles");
            Assert.That(material.shader.keywordSpace.FindKeyword("_THIN_WIRE_FEED").isValid, Is.True, "the feed's keyword is declared");
            Assert.That(material.IsKeywordEnabled("_THIN_WIRE_COVERAGE"), Is.True, "the sub-pixel coverage");
            Assert.That(material.IsKeywordEnabled("_THIN_WIRE_FEED"), Is.True, "the feed's sample spacing");
            Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
            Assert.That((Vector4)material.GetColor("_BaseColor"), Is.EqualTo(new Vector4(108 / 255f, 125 / 255f, 140 / 255f, 1f)).Using(new Vector4Near(1e-5f)),
                "the coil's fiber paint, #6c7d8c (drone_plastic's \"fiber\")");
        }

        // ---- checks ----

        static void Expect(FiberView view, Double3[] trail, int count, int builds, string what)
        {
            view.DrawTrail(trail, count);
            Assert.That(view.ChunkBuilds, Is.EqualTo(builds), what);
            Assert.That(TrailError(view, trail, count), Is.LessThan(1e-3), what + ": within 1 mm of the points");
        }

        static Double3 Zigzag(int i, int variant) =>
            new Double3(1500 + 0.7 * i, 0.01 * Math.Sin(i), -800 + 0.3 * i + (i % 2) * 0.2 + 0.05 * variant);

        // The span's rings against the pose's nodes (a ring's centre is its four vertices' mean); the rings past the count
        // collapse onto the last.
        static double SpanError(FiberView view, in PoseFrame pose)
        {
            MeshRenderer renderer = view.SpanRenderer;
            Assert.That(renderer.enabled, Is.EqualTo(pose.FiberNodeCount >= 2));
            Vector3[] v = renderer.GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector3 at = renderer.transform.position;
            double worst = 0;
            for (int i = 0; i < pose.FiberNodeCount; i++) worst = Math.Max(worst, Distance(at, v, i, pose.FiberNodes[i]));
            for (int i = pose.FiberNodeCount * Sides; i < v.Length; i++)
                Assert.That(v[i], Is.EqualTo(v[(pose.FiberNodeCount - 1) * Sides + i % Sides]), "a ring past the count collapses");
            return worst;
        }

        static double TrailError(FiberView view, Double3[] trail, int count)
        {
            double worst = 0;
            for (int k = 0; k < view.ChunkCount; k++)
            {
                MeshRenderer renderer = view.ChunkRenderer(k);
                int first = k * FiberView.ChunkPoints;
                Assert.That(renderer.enabled, Is.EqualTo(first <= count - 2), "chunk " + k + " drawn while it holds a segment");
                if (!renderer.enabled) continue;
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Vector3[] v = renderer.GetComponent<MeshFilter>().sharedMesh.vertices;
                Vector3 at = renderer.transform.position;
                for (int i = 0; i <= FiberView.ChunkPoints && first + i < count; i++) worst = Math.Max(worst, Distance(at, v, i, trail[first + i]));
            }
            return worst;
        }

        static double Distance(Vector3 at, Vector3[] v, int ring, in Double3 point)
        {
            Vector3 c = (v[ring * Sides] + v[ring * Sides + 1] + v[ring * Sides + 2] + v[ring * Sides + 3]) / Sides;
            double x = (double)at.x + c.x - point.X, y = (double)at.y + c.y - point.Y, z = (double)at.z + c.z - point.Z;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        sealed class Vector4Near : System.Collections.Generic.IEqualityComparer<Vector4>
        {
            readonly float _tolerance;
            public Vector4Near(float tolerance) => _tolerance = tolerance;
            public bool Equals(Vector4 a, Vector4 b) => (a - b).magnitude <= _tolerance;
            public int GetHashCode(Vector4 v) => 0;
        }
    }
}
