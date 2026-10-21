using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Fpv450.Feed.Tests
{
    /// <summary>
    /// The FPV camera rig and the feed pass's wiring (p4-feed task 1.2, FEED-02). The rendered checks, the still A/B
    /// and the lens scenario, are batch captures (<c>Fpv450.Feed.FeedCapture</c>); these hold what needs no GPU.
    /// </summary>
    public sealed class FpvCameraTests
    {
        static FeedSettings Asset => AssetDatabase.LoadAssetAtPath<FeedSettings>(FeedSetup.SettingsPath);

        /// FEED-02's pinned intrinsics: f ≈ 1.005 half-widths before k1 ≈ 0.33, about 106° across, 16:9.
        [Test]
        public void TheSettingsAreThePinnedIntrinsics()
        {
            FeedSettings s = Asset;
            Assert.That(s, Is.Not.Null, FeedSetup.SettingsPath);
            Assert.That(s.lens, Is.True);
            Assert.That(s.k1, Is.EqualTo(0.33f));
            Assert.That(s.focalHalfWidths, Is.EqualTo(1.005f));
            Assert.That(s.PictureHorizontalFovDeg, Is.EqualTo(106f).Within(0.5f));
            Assert.That(s.seed, Is.EqualTo(1));
        }

        /// The render covers the barrel-distorted picture's corners: 1 + k1 (1 + (9/16)²) half-widths, so its
        /// vertical field of view is 2 atan(coverage · 9/16 / f), and it is 16:9.
        [Test]
        public void TheRenderCoversTheLensCorners()
        {
            FeedSettings s = ScriptableObject.CreateInstance<FeedSettings>();
            try
            {
                Assert.That(s.Coverage, Is.EqualTo(1.4344f).Within(0.0001f));
                Assert.That(s.RenderVerticalFovDeg, Is.EqualTo(77.52f).Within(0.01f));
                Vector2Int size = s.SceneSize;
                Assert.That(size.x / (float)size.y, Is.EqualTo(16f / 9f).Within(0.002f));
                Assert.That(size.x, Is.EqualTo(Mathf.RoundToInt(s.sceneWidth * s.Coverage)));

                // The lens off is the Godot feed's read: no coverage and no distortion in the pass.
                s.lens = false;
                Assert.That(s.Coverage, Is.EqualTo(1f));
                Assert.That(s.LensVector, Is.EqualTo(new Vector4(0f, 1f, 16f / 9f, 0f)));
                Assert.That(s.SceneSize.x, Is.EqualTo(s.sceneWidth));
            }
            finally
            {
                Object.DestroyImmediate(s);
            }
        }

        /// The rig runs the scheduler at the settings' seed, takes the camera's field of view and target while it
        /// runs, puts the feed on its own output camera, and gives the camera back.
        [Test]
        public void TheRigTakesTheCameraAndGivesItBack()
        {
            var go = new GameObject("FpvCameraTest");
            FeedSettings s = ScriptableObject.CreateInstance<FeedSettings>();
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.fieldOfView = 60f;
                var rig = go.AddComponent<FpvCamera>();
                rig.settings = s;
                rig.Begin();
                Assert.That(rig.Running, Is.True);
                Assert.That(camera.fieldOfView, Is.EqualTo(s.RenderVerticalFovDeg).Within(1e-4f));
                Assert.That(camera.targetTexture, Is.Not.Null);
                Assert.That(new Vector2Int(camera.targetTexture.width, camera.targetTexture.height), Is.EqualTo(s.SceneSize));
                Assert.That(rig.Feed.Scene, Is.SameAs(camera.targetTexture));
                Assert.That(rig.Output.GetComponent<FeedOutput>(), Is.SameAs(rig.Feed));
                Assert.That(rig.Output.cullingMask, Is.EqualTo(0));
                Assert.That(rig.Output.depth, Is.GreaterThan(camera.depth));

                rig.Step(1365);
                Assert.That(rig.Feed.Uniforms.Field, Is.EqualTo(1365f));
                Assert.That(rig.Feed.Uniforms.TearAmp, Is.EqualTo(0.6356117f)); // seed 1's dropout tell (feedbench)
                rig.Step(600); // back: replayed from the seed
                Assert.That(rig.Events.Field, Is.EqualTo(600));

                rig.End();
                Assert.That(rig.Running, Is.False);
                Assert.That(camera.fieldOfView, Is.EqualTo(60f));
                Assert.That(camera.targetTexture, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(s);
            }
        }

        /// Every quality level's URP renderer carries the feed feature with the feed shader, and the shader has its
        /// three blits in order.
        [Test]
        public void EveryRendererCarriesTheFeed()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(FeedSetup.ShaderPath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.passCount, Is.EqualTo(3));
            foreach (string level in new[] { "Low", "Medium", "High" })
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>($"Assets/_Project/Settings/URP-{level}-Renderer.asset");
                FeedFeature feature = renderer.rendererFeatures.Find(f => f is FeedFeature) as FeedFeature;
                Assert.That(feature, Is.Not.Null, level);
                Assert.That(feature.Shader, Is.SameAs(shader), level);
                Assert.That(feature.isActive, Is.True, level);
            }
        }
    }
}
