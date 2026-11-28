using System.Linq;
using Fpv450.Feed;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The wet look's switch (w21-rain-on-camera 1.3): Sunny touches nothing, a rain draws URP/Lit through its wet twin with
    /// the original's keywords and marks the hard renderers, and Restore puts every original back; the shaders it draws
    /// compile; the committed variant collection holds every keyword set the project's URP/Lit materials and terrains use.
    /// </summary>
    public sealed class WetLookTests
    {
        GameObject _go;
        Material _lit;

        [SetUp]
        public void Make()
        {
            WetLook.Restore();
            _lit = new Material(Shader.Find(WetLook.LitShader)) { name = "wet_test" };
            _lit.EnableKeyword("_NORMALMAP");
            _go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _go.GetComponent<Renderer>().sharedMaterial = _lit;
        }

        [TearDown]
        public void Clean()
        {
            WetLook.Restore();
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_lit);
        }

        [Test]
        public void SunnyTouchesNothing()
        {
            Renderer r = _go.GetComponent<Renderer>();
            uint mask = r.renderingLayerMask;
            Vector4 wet = Shader.GetGlobalVector("_Fpv450Wet");
            WetLook.Apply(WetState.Dry, 0.001f, new[] { r });
            Assert.That(r.sharedMaterial, Is.SameAs(_lit));
            Assert.That(r.renderingLayerMask, Is.EqualTo(mask));
            Assert.That(Shader.IsKeywordEnabled(WetLook.Keyword), Is.False);
            Assert.That(Shader.GetGlobalVector("_Fpv450Wet"), Is.EqualTo(wet));
            Assert.That(WetLook.Armed, Is.False);
            Assert.That(WetLook.ExtinctionPerM, Is.EqualTo(0f));
        }

        [Test]
        public void RainDrawsTheTwinAndRestorePutsTheOriginalBack()
        {
            Renderer r = _go.GetComponent<Renderer>();
            uint mask = r.renderingLayerMask;
            WetLook.Apply(WetState.Saturated, 0.0014f, new[] { r });
            Material twin = r.sharedMaterial;
            Assert.That(twin, Is.Not.SameAs(_lit));
            Assert.That(twin.shader.name, Is.EqualTo(WetLook.WetLitShader));
            Assert.That(twin.IsKeywordEnabled("_NORMALMAP"), Is.True, "the twin keeps the original's keywords");
            Assert.That(twin.renderQueue, Is.EqualTo(_lit.renderQueue));
            Assert.That(r.renderingLayerMask, Is.EqualTo(mask | (1u << WetLook.HardLayer)));
            Assert.That(Shader.IsKeywordEnabled(WetLook.Keyword), Is.True);
            Vector4 wet = Shader.GetGlobalVector("_Fpv450Wet"), water = Shader.GetGlobalVector("_Fpv450WetWater");
            Assert.That(wet.x, Is.EqualTo(1f));
            Assert.That(wet.y, Is.EqualTo(1f), "Severe rain stands water");
            Assert.That(water.y, Is.EqualTo((float)WetOptics.InternalReflection));
            Assert.That(WetLook.ExtinctionPerM, Is.EqualTo(0.0014f));

            WetLook.Restore();
            Assert.That(r.sharedMaterial, Is.SameAs(_lit));
            Assert.That(r.renderingLayerMask, Is.EqualTo(mask));
            Assert.That(Shader.IsKeywordEnabled(WetLook.Keyword), Is.False);
            Assert.That(twin == null, Is.True, "the twin is released");
        }

        [Test]
        public void TheWetShadersCompile()
        {
            string[] names =
            {
                WetLook.WetLitShader, WetLook.WetTerrainShader, "Hidden/Fpv450/Wet/Terrain Lit (Add Pass)", "Hidden/Fpv450/Wet/Terrain Lit (Base Pass)",
                WetLook.ExtinctionShader, "Fpv450/LayeredSurface", "Fpv450/Leaf", "Fpv450/Impostor", "Fpv450/Plant Bark", "Fpv450/Plant Leaves",
                "Fpv450/Plant Grass", "Farm Land/Grass", "Farm Land/Tree Bark", "Farm Land/Tree Leaves",
            };
            foreach (string name in names)
            {
                Shader s = Shader.Find(name);
                Assert.That(s, Is.Not.Null, name);
                ShaderMessage[] errors = ShaderUtil.GetShaderMessages(s).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
                Assert.That(ShaderUtil.ShaderHasError(s), Is.False, $"{name}: {string.Join("; ", errors.Select(m => m.message))}");
            }
        }

        [Test]
        public void TheVariantsCoverTheMaterials()
        {
            var shaders = AssetDatabase.LoadAssetAtPath<WetShaders>(WetVariants.AssetPath);
            Assert.That(shaders, Is.Not.Null, $"{WetVariants.AssetPath}: run Fpv450.Feed.WetVariants.Write");
            Assert.That(shaders.lit, Is.Not.Null);
            Assert.That(shaders.terrain, Is.Not.Null);
            Assert.That(shaders.extinction, Is.Not.Null);
            ShaderVariantCollection variants = shaders.variants;
            Assert.That(variants, Is.Not.Null);
            foreach (string[] keywords in WetVariants.LitKeywordSets())
                Assert.That(variants.Contains(new ShaderVariantCollection.ShaderVariant(shaders.lit, PassType.ScriptableRenderPipeline, keywords)), Is.True,
                    $"the wet Lit with [{string.Join(" ", keywords)}]: run Fpv450.Feed.WetVariants.Write");
            Assert.That(variants.Contains(new ShaderVariantCollection.ShaderVariant(shaders.terrain, PassType.ScriptableRenderPipeline, "_NORMALMAP", "_MASKMAP")), Is.True);
        }
    }
}
