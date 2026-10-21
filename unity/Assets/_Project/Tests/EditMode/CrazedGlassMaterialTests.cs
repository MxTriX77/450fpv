using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The crazed-glass material (p1-r01-vaz-sedan, design decision 6; "Per-pane crack masks on a transparent
    /// material", scenarios "Crazed windscreen" and "Intact pane"): <c>Shaders/CrazedGlass</c> compiles as one
    /// transparent forward pass; the transparent recipe <c>glass</c> builds the plain glass; and through the importer's
    /// prefab step over the X-sign probe fixture, a pane whose metadata lists a crack mask draws with its own transparent
    /// material reading the mask over its UV rectangle, while an intact pane draws with the plain glass itself. A second
    /// build changes no file, a pane that is intact again loses its material, and a crack mask on an opaque recipe and
    /// a texture of an unknown role are refused. Everything is written under <see cref="Folder"/>, deleted before and
    /// after.
    /// </summary>
    public sealed class CrazedGlassMaterialTests
    {
        const string Folder = "Assets/_Project/Tests/EditMode/ThrowawayCrazedGlass";
        const string FixturePath = "Assets/_Project/Tests/EditMode/Fixtures/xsign_probe.fbx";
        const string ManifestFile = "tools/assets/manifest.json";
        const string GlassAlbedoPath = "Assets/_Project/Art/Materials/glass_albedo.png";
        const string MaskPath = Folder + "/probe_cracks.png";

        // The probe's one slot, as metadata schema 1; VARIANT and TEXTURES are filled in per build.
        const string Metadata = @"{
  ""schema"": 1, ""id"": ""T00"", ""variant"": ""VARIANT"", ""generator"": ""test"",
  ""params_sha256"": ""0"", ""units"": ""m"", ""frame"": ""unity-lh-yup"",
  ""bounds"": { ""min"": [0.0, 0.0, 0.0], ""max"": [0.3, 0.1, 0.2] },
  ""lods"": [ { ""mesh"": ""xsign_probe"", ""tris"": 12, ""switch_distance_m"": 0.0, ""thin_wire"": false } ],
  ""micro"": [],
  ""cull_distance_m"": 50.0,
  ""materials"": [ { ""slot"": ""probe"", ""visual"": ""glass"", ""physics"": ""glass"" } ],
  ""collision"": [ { ""shape"": ""box"", ""center"": [0.15, 0.05, 0.1], ""size"": [0.3, 0.1, 0.2],
                   ""rotation"": [0.0, 0.0, 0.0, 1.0], ""material"": ""glass"", ""landable"": false } ],
  ""gaps"": [], ""wind_volumes"": [], ""ports"": [], ""wires"": [], ""holes"": [], ""snag"": []TEXTURES
}
";
        // The mask over the pane's UVs from (0.1, -0.05) to (0.5, 0.15) m: scale 1 / (0.4, 0.2), offset -min x scale.
        const string Textures = @",
  ""textures"": [ { ""role"": ""crack_mask"", ""slot"": ""probe"", ""file"": ""probe_cracks.png"",
                  ""uv_min"": [0.1, -0.05], ""uv_max"": [0.5, 0.15] } ]";
        static readonly Vector2 MaskScale = new Vector2(2.5f, 5f), MaskOffset = new Vector2(-0.25f, 0.25f);

        [SetUp]
        public void SetUp() => DeleteThrowaway();

        [TearDown]
        public void TearDown() => DeleteThrowaway();

        static void DeleteThrowaway()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.DeleteAsset(Folder);
        }

        static Material Recipe(string recipe)
        {
            string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ManifestFile));
            var manifest = Json.Obj(Json.Parse(File.ReadAllText(manifestPath)), ManifestFile);
            Material material = CatalogImporter.EnsureMaterial(recipe, manifest, $"{Folder}/{recipe}.mat");
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>A small crack mask beside the metadata: one horizontal and one vertical line.</summary>
        static void WriteMask()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    byte v = x == size / 3 || y == size / 2 ? (byte)255 : (byte)0;
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(FullPath(MaskPath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>Writes the metadata and runs the importer's prefab step over the probe FBX with its slot on <paramref name="glass"/>.</summary>
        static GameObject Build(string variant, bool crazed, Material glass) => Build(variant, crazed ? Textures : "", glass);

        static GameObject Build(string variant, string textures, Material glass)
        {
            string metaPath = $"{Folder}/{variant}.assetmeta.json";
            File.WriteAllText(FullPath(metaPath), Metadata.Replace("VARIANT", variant).Replace("TEXTURES", textures));
            AssetDatabase.ImportAsset(metaPath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(metaPath);
            Assert.That(source, Is.Not.Null, "the metadata imported as a TextAsset");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(FixturePath), Is.Not.Null, $"{FixturePath} is missing: run tools/blender/tests/make_xsign_probe.py in Blender");

            string prefabPath = $"{Folder}/{variant}.prefab";
            CatalogImporter.EnsurePrefab(prefabPath, variant, Json.Obj(Json.Parse(source.text), metaPath), source, FixturePath,
                new Dictionary<string, Material> { ["probe"] = glass });
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        static Material PaneMaterial(GameObject prefab) => prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;

        [Test]
        public void Shader_CompilesAsOneTransparentForwardPass()
        {
            Shader shader = Shader.Find(CatalogImporter.CrazedGlassShader);
            Assert.That(shader, Is.Not.Null, "Shaders/CrazedGlass.shader");
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "the shader compiles");
            // The declared subshader (the fallback's follow it), not the active one: batchmode without a GPU falls back.
            Assert.That(shader.FindSubshaderTagValue(0, new ShaderTagId("RenderPipeline")).name, Is.EqualTo("UniversalPipeline"));
            Assert.That(shader.FindSubshaderTagValue(0, new ShaderTagId("Queue")).name, Is.EqualTo("Transparent"));
            Assert.That(shader.GetPassCountInSubshader(0), Is.EqualTo(1), "the forward pass alone: no shadow caster, no depth");
            Assert.That(shader.FindPassTagValue(0, 0, new ShaderTagId("LightMode")).name, Is.EqualTo("UniversalForward"));
            Assert.That(shader.FindPropertyIndex("_CrackMask"), Is.GreaterThanOrEqualTo(0), "the crack mask's property");
        }

        [Test]
        public void TransparentRecipe_BuildsThePlainGlass()
        {
            Material glass = Recipe("glass");

            Assert.That(glass.shader.name, Is.EqualTo(CatalogImporter.CrazedGlassShader));
            Assert.That(glass.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
            Assert.That(glass.mainTexture, Is.Not.Null.And.EqualTo(AssetDatabase.LoadAssetAtPath<Texture2D>(GlassAlbedoPath)),
                "the palette's albedo (the review refuses a material without one)");
            Color tint = glass.GetColor("_BaseColor"); // the recipe's tint, its alpha each face's opacity (stored through a colour-space round trip)
            Assert.That(new[] { tint.r, tint.g, tint.b, tint.a }, Is.EqualTo(new[] { 0.15f, 0.15f, 0.15f, 0.02f }).Within(1e-5f), "the recipe's tint");
            Assert.That(glass.GetFloat("_Smoothness"), Is.EqualTo(0.95f));
            Assert.That(glass.GetFloat("_IOR"), Is.EqualTo(1.52f));
            Assert.That(glass.GetTexture("_CrackMask"), Is.Null, "no cracks on the plain glass");
        }

        [Test]
        public void PaneWithAMask_GetsItsOwnTransparentMaterial_AndAnIntactPaneThePlainGlass()
        {
            Material glass = Recipe("glass");
            WriteMask();

            Material crazed = PaneMaterial(Build("pane_crazed", true, glass));
            Material intact = PaneMaterial(Build("pane_intact", false, glass));

            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);
            Assert.That(AssetDatabase.GetAssetPath(crazed), Is.EqualTo($"{Folder}/pane_crazed_probe_crazed.mat"), "its own material, beside the prefab");
            Assert.That(crazed.shader.name, Is.EqualTo(CatalogImporter.CrazedGlassShader));
            Assert.That(crazed.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
            Assert.That(crazed.GetTexture("_CrackMask"), Is.Not.Null.And.EqualTo(mask), "it reads its crack mask");
            Vector2 scale = crazed.GetTextureScale("_CrackMask"), offset = crazed.GetTextureOffset("_CrackMask");
            Assert.That(scale.x, Is.EqualTo(MaskScale.x).Within(1e-4f), "mask scale u");
            Assert.That(scale.y, Is.EqualTo(MaskScale.y).Within(1e-4f), "mask scale v");
            Assert.That(offset.x, Is.EqualTo(MaskOffset.x).Within(1e-4f), "mask offset u");
            Assert.That(offset.y, Is.EqualTo(MaskOffset.y).Within(1e-4f), "mask offset v");
            Assert.That(crazed.mainTexture, Is.EqualTo(glass.mainTexture), "the plain glass's body");
            Assert.That(crazed.GetColor("_BaseColor"), Is.EqualTo(glass.GetColor("_BaseColor")));
            Assert.That(crazed.GetFloat("_Smoothness"), Is.EqualTo(glass.GetFloat("_Smoothness")));
            Assert.That(crazed.GetFloat("_IOR"), Is.EqualTo(glass.GetFloat("_IOR")));

            Assert.That(intact, Is.EqualTo(glass), "an intact pane draws with the plain glass");
            Assert.That(intact.GetTexture("_CrackMask"), Is.Null);

            var importer = (TextureImporter)AssetImporter.GetAtPath(MaskPath);
            Assert.That(importer.sRGBTexture, Is.False, "the mask is linear data");
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), "no cracks outside the mask");
        }

        [Test]
        public void SecondBuild_ChangesNoFile_AndAPaneIntactAgainLosesItsMaterial()
        {
            Material glass = Recipe("glass");
            WriteMask();
            Build("pane", true, glass);
            Dictionary<string, byte[]> first = Snapshot();
            Assert.That(first.Keys, Does.Contain("pane_probe_crazed.mat"));

            Build("pane", true, glass);
            Dictionary<string, byte[]> second = Snapshot();
            Assert.That(second.Keys, Is.EquivalentTo(first.Keys), "the same files");
            foreach (KeyValuePair<string, byte[]> file in first)
                Assert.That(second[file.Key], Is.EqualTo(file.Value), $"{file.Key} changed");

            Material intact = PaneMaterial(Build("pane", false, glass));
            Assert.That(intact, Is.EqualTo(glass), "intact again: the plain glass");
            Assert.That(Snapshot().Keys, Does.Not.Contain("pane_probe_crazed.mat"), "the crazed build's pane material is gone");
        }

        [Test]
        public void CrackMaskOnAnOpaqueRecipe_IsRefused()
        {
            Material opaque = Recipe("wire_sip"); // a URP/Lit palette
            WriteMask();

            var refused = Assert.Throws<FormatException>(() => Build("pane_opaque", true, opaque));
            Assert.That(refused.Message, Does.Contain("not transparent"));
        }

        /// <summary>A texture of a role the importer does not know is refused (a damage mask binds a layered surface: LayeredSurfaceMaterialTests).</summary>
        [Test]
        public void UnknownTextureRole_IsRefused()
        {
            Material opaque = Recipe("wire_sip");
            WriteMask();
            const string other = @",
  ""textures"": [ { ""role"": ""normal_map"", ""slot"": ""probe"", ""file"": ""probe_cracks.png"",
                  ""uv_min"": [0.1, -0.05], ""uv_max"": [0.5, 0.15] } ]";

            var refused = Assert.Throws<FormatException>(() => Build("pane_other", other, opaque));
            Assert.That(refused.Message, Does.Contain("unknown texture role \"normal_map\""));
        }

        static Dictionary<string, byte[]> Snapshot() =>
            Directory.GetFiles(FullPath(Folder)).ToDictionary(file => Path.GetFileName(file), file => File.ReadAllBytes(file));

        static string FullPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }
}
