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
    /// (p1-s04-belt-straw design 7): a surfaces asset is shot from on the surface, every other class as before.
    /// </summary>
    public sealed class ReviewCaptureTests
    {
        const string BrickMaterialPath = "Assets/_Project/Art/Materials/brick_red.mat";

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

        static Review.Stage BuildStage(string prefabPath, string metaPath, string assetClass, string id)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Not.Null, $"{prefabPath} is missing: run pwsh tools/build_asset.ps1 {id}");
            return Review.Stage.Build(prefabPath, ReviewMeta.Read(Path.Combine(Application.dataPath, metaPath)), assetClass);
        }

        static void AssertNear(Vector3 actual, Vector3 expected, string what) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f), $"{what} {actual:F4}, expected {expected:F4}");
    }
}
