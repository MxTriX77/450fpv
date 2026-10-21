using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The review capture's empty-stage check (p1-pipeline-hardening, "An empty stage fails the capture"): a prefab
    /// whose renderers have no mesh or whose materials have no texture asset, as an un-smudged LFS pointer leaves
    /// them, is refused by name before any tile is rendered. The exit path is <see cref="Review.Capture"/>'s catch;
    /// here the check is exercised through the method on throwaway objects. And the pilot-height pose
    /// (p1-s04-belt-straw design 7): a surfaces asset is shot from on the surface, an earthwork with a holes record
    /// along its run (p1-e01-trench design 9), every other class as before. And the stage's shadow-distance fit,
    /// shared with the LOD parity measure (p1-v01-locust task 1.2h).
    /// </summary>
    public sealed class ReviewCaptureTests
    {
        const string BrickMaterialPath = "Assets/_Project/Art/Materials/brick_red.mat";

        /// <summary>
        /// asset-pipeline "Per-variant material overrides": the review capture shows a variant's materials, so a legend
        /// line longer than the panel breaks after its commas and keeps every slot (p1-v01-locust task 1.4).
        /// </summary>
        [Test]
        public void Legend_BreaksALongLineAfterItsCommasAndKeepsEverySlot()
        {
            const string line = "MATERIALS bark > bark_locust / bark, twig > bark_locust / twig, leaf > leaf_locust_autumn / twig, pod > pod_locust / twig";
            string[] rows = System.Linq.Enumerable.ToArray(Review.Wrap(line, 54));
            Assert.That(rows.Length, Is.GreaterThan(1));
            Assert.That(rows, Has.All.Length.LessThanOrEqualTo(54));
            Assert.That(string.Join(" ", rows).Replace(",   ", ", "), Is.EqualTo(line));
            Assert.That(rows, Has.Some.Contains("leaf > leaf_locust_autumn / twig"));
            Assert.That(Review.Wrap("BOUNDS 1 X 2 X 3 M", 54), Is.EqualTo(new[] { "BOUNDS 1 X 2 X 3 M" }));
        }

        [Test]
        public void RequireRenderable_NamesTheMissingMeshAndTexture()
        {
            var root = new GameObject("throwaway");
            var textureless = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                var lod = new GameObject("throwaway_LOD0", typeof(MeshFilter), typeof(MeshRenderer));
                lod.transform.SetParent(root.transform, false);
                lod.GetComponent<MeshRenderer>().sharedMaterial = textureless;

                var error = Assert.Throws<InvalidOperationException>(() => Review.RequireRenderable(root, "throwaway"));
                Assert.That(error.Message, Does.StartWith("throwaway: "));
                Assert.That(error.Message, Does.Contain("no mesh on throwaway_LOD0"));
                Assert.That(error.Message, Does.Contain($"texture missing on {textureless.name}"));
                Assert.That(error.Message, Does.Contain("git lfs checkout"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(textureless);
            }
        }

        [Test]
        public void RequireRenderable_RefusesATextureThatIsNotAnAsset()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var runtimeTexture = new Texture2D(2, 2);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { mainTexture = runtimeTexture };
            try
            {
                cube.GetComponent<MeshRenderer>().sharedMaterial = material;
                var error = Assert.Throws<InvalidOperationException>(() => Review.RequireRenderable(cube, "cube"));
                Assert.That(error.Message, Does.Contain($"texture missing on {material.name}"));
                Assert.That(error.Message, Does.Not.Contain("no mesh"));
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(runtimeTexture);
            }
        }

        [Test]
        public void RequireRenderable_PassesAMeshWithATexturedMaterial()
        {
            var brick = AssetDatabase.LoadAssetAtPath<Material>(BrickMaterialPath);
            Assert.That(brick, Is.Not.Null, $"{BrickMaterialPath} is missing: run pwsh tools/build_asset.ps1 K01");
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                cube.GetComponent<MeshRenderer>().sharedMaterial = brick;
                Assert.DoesNotThrow(() => Review.RequireRenderable(cube, "cube"));
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }

        /// <summary>
        /// p1-s04-belt-straw design 7, "Pilot-height tile of a ground patch": a surfaces asset is shot from (0, 1.5, 0)
        /// in the patch's frame (its origin, the centre on the mat's mean top), 35° down towards +X, with the bar planted
        /// at (2.5, 0, 0) and in frame. Needs a prior <c>pwsh tools/build_asset.ps1 S04</c>.
        /// </summary>
        [Test]
        public void PilotPose_Surface_StandsOnThePatch_LooksDown35DegreesTowardsPlusX_WithTheBarInFrame()
        {
            Review.Stage stage = BuildStage("Assets/_Project/Prefabs/surfaces/S04_patch.prefab", "_Project/Art/Models/surfaces/S04/S04_patch.assetmeta.json", "surfaces", "S04");
            Review.Rig rig = Review.Rig.Create();
            try
            {
                (Vector3 position, Vector3 lookAt, Vector3 barBase, bool fromBounds) = Review.PilotPose(stage);
                Matrix4x4 patch = stage.LocalToWorld;
                AssertNear(position, patch.MultiplyPoint3x4(new Vector3(0f, 1.5f, 0f)), "camera");
                AssertNear(barBase, patch.MultiplyPoint3x4(new Vector3(2.5f, 0f, 0f)), "bar base");
                Assert.That(fromBounds, Is.False);

                rig.Aim(position, lookAt);
                float down = 35f * Mathf.Deg2Rad;
                Vector3 forward = rig.Camera.transform.forward;
                Assert.That(Vector3.Angle(forward, new Vector3(Mathf.Cos(down), -Mathf.Sin(down), 0f)), Is.LessThan(0.1f), $"forward {forward:F4}");
                Assert.That(rig.InFrame(barBase), Is.True, "the bar's foot is in frame");
                Assert.That(rig.InFrame(barBase + Vector3.up), Is.True, "the bar's lower metre is in frame");
            }
            finally
            {
                rig.Dispose();
                Object.DestroyImmediate(stage.Root);
            }
        }

        /// <summary>
        /// "Other classes unchanged": K01 (debris) keeps the pose from before p1-s04-belt-straw, beside its bounds. The
        /// numbers are the old PilotShot and PilotBar on K01's 0.25 × 0.065 × 0.12 m bounds; the capture before the
        /// change logged the camera at (-0.22, 1.50, -0.22).
        /// </summary>
        [Test]
        public void PilotPose_K01_IsThePoseFromBeforeTheSurfaceFraming()
        {
            Review.Stage stage = BuildStage("Assets/_Project/Prefabs/debris/K01_whole.prefab", "_Project/Art/Models/debris/K01/K01_whole.assetmeta.json", "debris", "K01");
            try
            {
                (Vector3 position, Vector3 lookAt, Vector3 barBase, bool fromBounds) = Review.PilotPose(stage);
                AssertNear(position, new Vector3(-0.2196f, 1.5f, -0.2196f), "camera");
                AssertNear(lookAt, new Vector3(0f, 0.0325f, 0f), "look-at point");
                AssertNear(barBase, new Vector3(0.4312f, 0f, 0.4312f), "bar base");
                Assert.That(fromBounds, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(stage.Root);
            }
        }

        /// <summary>
        /// The stage's shadow-distance fit, which the LOD parity measure shares (p1-v01-locust task 1.2h): four turntable
        /// distances, at least 12 m, never over the game's. The numbers were computed apart from the code, in double
        /// precision, from each stage's bounds and the bar 1 m outside them at 112.5°: K01's frame, extents (0.6110, 0.9,
        /// 0.2645) m, gives a 2.462894 m turntable and the 12 m floor; S04's, (6.4860, 0.9, 6.0) m, a 19.538929 m
        /// turntable, whose four, 78.155714 m, the game's 50 m caps. Needs prior builds of K01 and S04.
        /// </summary>
        [Test]
        public void ShadowDistance_IsFourTurntableDistances_AtLeast12M_NeverOverTheGames()
        {
            Review.Stage k01 = BuildStage("Assets/_Project/Prefabs/debris/K01_whole.prefab", "_Project/Art/Models/debris/K01/K01_whole.assetmeta.json", "debris", "K01");
            Review.Stage s04 = BuildStage("Assets/_Project/Prefabs/surfaces/S04_patch.prefab", "_Project/Art/Models/surfaces/S04/S04_patch.assetmeta.json", "surfaces", "S04");
            try
            {
                Assert.That(Review.TurntableDistance(k01), Is.EqualTo(2.462894f).Within(1e-4f));
                Assert.That(Review.ShadowDistance(k01, 50f), Is.EqualTo(12f));
                Assert.That(Review.ShadowDistance(k01, 10f), Is.EqualTo(10f));
                Assert.That(Review.TurntableDistance(s04), Is.EqualTo(19.538929f).Within(1e-4f));
                Assert.That(Review.ShadowDistance(s04, 50f), Is.EqualTo(50f));
                Assert.That(Review.ShadowDistance(s04, 100f), Is.EqualTo(78.155714f).Within(1e-3f));
            }
            finally
            {
                Object.DestroyImmediate(k01.Root);
                Object.DestroyImmediate(s04.Root);
            }
        }

        /// <summary>
        /// p1-l01-l03-pole-wires task 1.6: the stage lifts an asset whose bounds reach below y = 0 onto the ground and
        /// leaves one whose lowest point is above it at its own height (a lift of max(0, -min.y)). The placement reads
        /// only the metadata's bounds, so literal bounds on K01's prefab: S04's 0.425 m below the ground, and a span
        /// from 3.5 m to 6.5 m up like L03's.
        /// </summary>
        [TestCase(-0.425f, 0.425f, 0f, TestName = "StageBuild_BoundsBelowTheGround_AreLiftedOntoIt")]
        [TestCase(3.5f, 0f, 3.5f, TestName = "StageBuild_BoundsAboveTheGround_StayAtTheirHeight")]
        public void StageBuild_LiftsOnlyWhatReachesBelowTheGround(float minY, float lift, float lowestOnStage)
        {
            const string prefabPath = "Assets/_Project/Prefabs/debris/K01_whole.prefab";
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Not.Null, $"{prefabPath} is missing: run pwsh tools/build_asset.ps1 K01");
            var meta = new ReviewMeta { Bounds = new Bounds(new Vector3(10f, minY + 1.5f, 0f), new Vector3(20f, 3f, 0.5f)) };
            Review.Stage stage = Review.Stage.Build(prefabPath, meta, "lines");
            try
            {
                AssertNear(stage.Origin, new Vector3(0f, lift, 0f), "asset origin");
                Assert.That(stage.AssetBounds.min.y, Is.EqualTo(lowestOnStage).Within(1e-5f), "lowest point on the stage");
                AssertNear(stage.AssetBounds.size, new Vector3(20f, 3f, 0.5f), "bounds size");
            }
            finally
            {
                Object.DestroyImmediate(stage.Root);
            }
        }

        // p1-e01-trench design 9: hand-made metadata, the run first and a niche after it, square to the run, which the
        // capture ignores. The straight run lies on the lip along +X; the bent one starts at (2, 1), runs (0.6, 0.8) for
        // two metres and bends 25° left, with its points at y = -1.5 to show the lip is taken at y = 0 whatever their height.
        const string StraightRun = @"{""holes"": [
            {""id"": ""straight"", ""centre_line"": [[0, 0, 0], [1, 0, 0], [2, 0, 0], [3, 0, 0], [4, 0, 0]], ""top_width_m"": 0.8, ""depth_m"": 1.5, ""band_width_m"": 2.3},
            {""id"": ""straight_niche"", ""centre_line"": [[2, 0, 0], [2, 0, -1.1]], ""top_width_m"": 0.8, ""depth_m"": 1.35, ""band_width_m"": 2.1}]}";
        const string BentRun = @"{""holes"": [
            {""id"": ""bent"", ""centre_line"": [[2, -1.5, 1], [2.6, -1.5, 1.8], [3.2, -1.5, 2.6], [3.406, -1.5, 3.579], [3.611, -1.5, 4.557]], ""top_width_m"": 1.2, ""depth_m"": 1.5, ""band_width_m"": 2.7},
            {""id"": ""bent_niche"", ""centre_line"": [[2.6, -1.5, 1.8], [3.48, -1.5, 1.14]], ""top_width_m"": 0.8, ""depth_m"": 1.35, ""band_width_m"": 2.1}]}";

        /// <summary>
        /// p1-e01-trench design 9, "Pilot-height tile of a trench", on a straight run: the camera 1.5 m above the lip at
        /// the run's start, half the 0.8 m top width out to the run's right (the field side), so (0, 1.5, -0.4) in the
        /// asset's frame; forward along +X and 20° down; the bar 3 m along the run and 0.2 m out from the lip, (3, 0, -0.6).
        /// The stage lifts the asset by the fixture bounds' 1.8 m below the ground.
        /// </summary>
        [Test]
        public void PilotPose_Earthworks_StraightRun_StandsAboveTheLip_LooksAlongIt20DegreesDown()
        {
            AssertRunPose(StraightRun, new Vector3(0f, 3.3f, -0.4f), new Vector3(0.93969f, -0.34202f, 0f), new Vector3(3f, 1.8f, -0.6f));
        }

        /// <summary>
        /// The same on a bent run: the first segment's plan direction (0.6, 0, 0.8), not the run's overall one; its right
        /// (0.8, 0, -0.6) times half the 1.2 m top width from (2, 0, 1) puts the camera at (2.48, 1.5, 0.64), and the bar at
        /// (2, 0, 1) + 3 (0.6, 0, 0.8) + 0.8 (0.8, 0, -0.6) = (4.44, 0, 2.92).
        /// </summary>
        [Test]
        public void PilotPose_Earthworks_BentRun_LooksAlongTheFirstSegment()
        {
            AssertRunPose(BentRun, new Vector3(2.48f, 3.3f, 0.64f), new Vector3(0.56382f, -0.34202f, 0.75175f), new Vector3(4.44f, 1.8f, 2.92f));
        }

        /// <summary>An earthwork without a <c>holes</c> record keeps the pose beside its bounds.</summary>
        [TestCase("{}", TestName = "EarthworkRun_NoHolesKey_IsNone")]
        [TestCase(@"{""holes"": []}", TestName = "EarthworkRun_EmptyHoles_IsNone")]
        public void EarthworkRun_WithoutAHolesRecord_IsNone(string fixture)
        {
            Assert.That(Review.EarthworkRun.Read(fixture, "fixture"), Is.Null);
        }

        static void AssertRunPose(string fixture, Vector3 camera, Vector3 forward, Vector3 barBase)
        {
            const string prefabPath = "Assets/_Project/Prefabs/debris/K01_whole.prefab";
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Not.Null, $"{prefabPath} is missing: run pwsh tools/build_asset.ps1 K01");
            var meta = new ReviewMeta { Bounds = new Bounds(new Vector3(12f, -0.6f, 0f), new Vector3(36f, 2.4f, 14f)) };
            Review.Stage stage = Review.Stage.Build(prefabPath, meta, "earthworks", Review.EarthworkRun.Read(fixture, "fixture"));
            Review.Rig rig = Review.Rig.Create();
            try
            {
                (Vector3 position, Vector3 lookAt, Vector3 bar, bool fromBounds) = Review.PilotPose(stage);
                AssertNear(position, camera, "camera");
                AssertNear(bar, barBase, "bar base");
                Assert.That(fromBounds, Is.False);

                rig.Aim(position, lookAt);
                Vector3 actual = rig.Camera.transform.forward;
                Assert.That(Vector3.Angle(actual, forward), Is.LessThan(0.1f), $"forward {actual:F4}, expected {forward:F4}");
                Assert.That(rig.InFrame(bar), Is.True, "the bar's foot is in frame");
                Assert.That(rig.InFrame(bar + Vector3.up * 1.8f), Is.True, "the bar's top is in frame");
            }
            finally
            {
                rig.Dispose();
                Object.DestroyImmediate(stage.Root);
            }
        }

        static Review.Stage BuildStage(string prefabPath, string metaPath, string assetClass, string id)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Not.Null, $"{prefabPath} is missing: run pwsh tools/build_asset.ps1 {id}");
            return Review.Stage.Build(prefabPath, ReviewMeta.Read(Path.Combine(Application.dataPath, metaPath)), assetClass);
        }

        static void AssertNear(Vector3 actual, Vector3 expected, string what) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f), $"{what} {actual:F4}, expected {expected:F4}");
    }
}
