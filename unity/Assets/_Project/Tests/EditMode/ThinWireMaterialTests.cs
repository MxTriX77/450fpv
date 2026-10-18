using System.IO;
using Fpv450.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The thin-wire material (p1-l01-l03-pole-wires, design decision 8, scenario "Nearly invisible, never
    /// shimmering"): <c>Shaders/ThinWire</c> compiles, declares its coverage keyword and draws in one alpha-blended
    /// forward pass with no shadow caster; the importer turns a wire recipe's URP/Lit material into a thin-wire
    /// material beside it with the keyword on and the recipe's surface, and a second build changes no file. Built from
    /// <c>wire_steel</c> (a tiled set with a packed mask) and <c>wire_sip</c> (a flat palette) under
    /// <see cref="Folder"/>, deleted before and after.
    /// </summary>
    public sealed class ThinWireMaterialTests
    {
        const string Folder = "Assets/_Project/Tests/EditMode/ThrowawayThinWire";
        const string ManifestFile = "tools/assets/manifest.json";

        [SetUp]
        public void SetUp() => DeleteThrowaway();

        [TearDown]
        public void TearDown() => DeleteThrowaway();

        static void DeleteThrowaway()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.DeleteAsset(Folder);
        }

        static Material Build(string recipe)
        {
            string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ManifestFile));
            var manifest = Json.Obj(Json.Parse(File.ReadAllText(manifestPath)), ManifestFile);
            Material surface = CatalogImporter.EnsureMaterial(recipe, manifest, $"{Folder}/{recipe}.mat");
            Material wire = CatalogImporter.EnsureThinWireMaterial(surface);
            AssetDatabase.SaveAssets();
            return wire;
        }

        [Test]
        public void Shader_CompilesWithItsKeywordAndNoShadowCaster()
        {
            Shader shader = Shader.Find(CatalogImporter.ThinWireShader);
            Assert.That(shader, Is.Not.Null, "Shaders/ThinWire.shader");
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "the shader compiles");
            Assert.That(shader.keywordSpace.FindKeyword(CatalogImporter.ThinWireKeyword).isValid, Is.True, "the coverage keyword is declared");
            // The declared subshader (the fallback's follow it), not the active one: batchmode without a GPU falls back.
            Assert.That(shader.FindSubshaderTagValue(0, new ShaderTagId("RenderPipeline")).name, Is.EqualTo("UniversalPipeline"));
            Assert.That(shader.FindSubshaderTagValue(0, new ShaderTagId("Queue")).name, Is.EqualTo("Transparent"));
            Assert.That(shader.GetPassCountInSubshader(0), Is.EqualTo(1), "one pass: no shadow caster, no depth");
            Assert.That(shader.FindPassTagValue(0, 0, new ShaderTagId("LightMode")).name, Is.EqualTo("UniversalForward"));
        }

        [TestCase("wire_steel")]
        [TestCase("wire_sip")]
        public void WireRecipe_GetsTheThinWireMaterialWithItsSurface(string recipe)
        {
            Material wire = Build(recipe);
            var surface = AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/{recipe}.mat");

            Assert.That(AssetDatabase.GetAssetPath(wire), Is.EqualTo($"{Folder}/{recipe}_thin_wire.mat"), "beside the recipe's material");
            Assert.That(wire.shader.name, Is.EqualTo(CatalogImporter.ThinWireShader));
            Assert.That(wire.IsKeywordEnabled(CatalogImporter.ThinWireKeyword), Is.True, "coverage on");
            Assert.That(wire.GetFloat("_ThinWireCoverage"), Is.EqualTo(1f), "the inspector toggle agrees with the keyword");
            Assert.That(wire.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
            Assert.That(surface.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), "the recipe's own material stays URP/Lit");

            Assert.That(wire.mainTexture, Is.Not.Null.And.EqualTo(surface.GetTexture("_BaseMap")), "the recipe's base map (the review refuses a material without one)");
            Assert.That(wire.GetTextureScale("_BaseMap"), Is.EqualTo(surface.GetTextureScale("_BaseMap")));
            Assert.That(wire.GetTextureOffset("_BaseMap"), Is.EqualTo(surface.GetTextureOffset("_BaseMap")));
            Assert.That(wire.GetColor("_BaseColor"), Is.EqualTo(surface.GetColor("_BaseColor")), "the recipe's tint");
            Assert.That(wire.GetFloat("_Smoothness"), Is.EqualTo(surface.GetFloat("_Smoothness")));
            Assert.That(wire.GetTexture("_MetallicGlossMap"), Is.EqualTo(surface.GetTexture("_MetallicGlossMap")));
            Assert.That(wire.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.EqualTo(surface.IsKeywordEnabled("_METALLICSPECGLOSSMAP")), "the packed mask's smoothness");
        }

        [Test]
        public void SecondBuild_ChangesNoFile()
        {
            Material wire = Build("wire_steel");
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetDatabase.GetAssetPath(wire)));
            byte[] first = File.ReadAllBytes(file);

            Build("wire_steel");

            Assert.That(File.ReadAllBytes(file), Is.EqualTo(first));
        }
    }
}
