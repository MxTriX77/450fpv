using System;
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
    /// here the check is exercised through the method on throwaway objects.
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
    }
}
