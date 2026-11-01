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
    /// The layered surface (p1-r01-vaz-sedan task 1.3b, design decision 5; p1-h01-adobe-house design decision 2):
    /// <c>Shaders/LayeredSurface</c> compiles with URP/Lit's four passes; through the importer's prefab step over the
    /// X-sign probe fixture, a slot whose metadata lists a damage mask draws with its own layered material, the slot's
    /// recipe (the car paint) as the base and the recipe's layers over it, and a slot without one keeps its recipe's
    /// material. Which layer a mask picks is read from the material by the shader's coverage rule (its header; the
    /// batchmode run has no GPU to render with): the car paint's R, G, B and A at 1 each pick their layer (rust, soot,
    /// dust, burnt-off steel) and at 0 the paint; H01's facade and interior, on their own recipes and sets, lie over
    /// their whitewash base, R at 0, 0.5 and 1 picks whitewash, render and adobe, A lays the band over the render and
    /// under the adobe, and an unmasked facade keeps its bare adobe (p1-h01-adobe-house task 1.2). A second build
    /// changes no file, a slot whose mask is gone loses its layered material, and malformed layers and a mask whose
    /// channels are not its recipe's are refused. Everything is written under <see cref="Folder"/>, deleted before and
    /// after; the layers' own recipe materials are the project's, which the importer keeps up to date in place (those a
    /// facade build creates are deleted after it: H01's build commits its own).
    /// </summary>
    public sealed class LayeredSurfaceMaterialTests
    {
        const string Folder = "Assets/_Project/Tests/EditMode/ThrowawayLayeredSurface";
        const string FixturePath = "Assets/_Project/Tests/EditMode/Fixtures/xsign_probe.fbx";
        const string ManifestFile = "tools/assets/manifest.json";
        const string MaterialsFolder = "Assets/_Project/Art/Materials";
        const string MaskPath = Folder + "/probe_damage.png";

        // The probe's one slot, as metadata schema 1; VARIANT, VISUAL and TEXTURES are filled in per build.
        const string Metadata = @"{
  ""schema"": 1, ""id"": ""T00"", ""variant"": ""VARIANT"", ""generator"": ""test"",
  ""params_sha256"": ""0"", ""units"": ""m"", ""frame"": ""unity-lh-yup"",
  ""bounds"": { ""min"": [0.0, 0.0, 0.0], ""max"": [0.3, 0.1, 0.2] },
  ""lods"": [ { ""mesh"": ""xsign_probe"", ""tris"": 12, ""switch_distance_m"": 0.0, ""thin_wire"": false } ],
  ""micro"": [],
  ""cull_distance_m"": 50.0,
  ""materials"": [ { ""slot"": ""probe"", ""visual"": ""VISUAL"", ""physics"": ""sheet_metal"" } ],
  ""collision"": [ { ""shape"": ""box"", ""center"": [0.15, 0.05, 0.1], ""size"": [0.3, 0.1, 0.2],
                   ""rotation"": [0.0, 0.0, 0.0, 1.0], ""material"": ""sheet_metal"", ""landable"": false } ],
  ""gaps"": [], ""wind_volumes"": [], ""ports"": [], ""wires"": [], ""holes"": [], ""snag"": []TEXTURES
}
";
        // The mask over the slot's UVs from (-2, 1) to (6, 5) m: scale 1 / (8, 4), offset -min x scale; CHANNELS is the
        // record's optional "channels".
        const string Textures = @",
  ""textures"": [ { ""role"": ""damage_mask"", ""slot"": ""probe"", ""file"": ""probe_damage.png"",
                  ""uv_min"": [-2.0, 1.0], ""uv_max"": [6.0, 5.0]CHANNELS } ]";
        static readonly Vector2 MaskScale = new Vector2(0.125f, 0.25f), MaskOffset = new Vector2(0.25f, -0.25f);

        // The car paint's layers by the mask channel that picks each (assets-src/materials/car_paint.json).
        static readonly (int channel, string recipe)[] PaintLayers = { (1, "car_burnt"), (3, "car_bare"), (0, "car_rust"), (2, "car_dust") };
        static readonly string[] ChannelNames = { "R", "G", "B", "A" };

        // H01's facade layers in order (assets-src/materials/facade_h01.json, its design decision 2) and what its mask's
        // channels hold, as the house generator's record names them.
        static readonly string[] FacadeLayers = { "clay_render", "base_band", "adobe", "wall_soot", "wall_dirt" };
        const int Render = 1, Band = 2, Adobe = 3, Soot = 4, Dirt = 5;
        const string FacadeChannels = @"[""render_depth"", ""soot"", ""dirt"", ""base_band""]";
        const string AdobeDiffuse = "Assets/_Project/Art/ThirdParty/polyhaven/dirt_floor/dirt_floor_diff_1k.png";

        // The project's recipe materials a facade build created, deleted once the fixture is done (never a deleted path reused within it).
        static readonly HashSet<string> MadeInProject = new HashSet<string>();

        [SetUp]
        public void SetUp() => DeleteThrowaway();

        [TearDown]
        public void TearDown() => DeleteThrowaway();

        [OneTimeTearDown]
        public void DeleteMadeInProject()
        {
            foreach (string path in MadeInProject)
                AssetDatabase.DeleteAsset(path);
            MadeInProject.Clear();
        }

        static void DeleteThrowaway()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.DeleteAsset(Folder);
        }

        static Dictionary<string, object> Manifest()
        {
            string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ManifestFile));
            return Json.Obj(Json.Parse(File.ReadAllText(manifestPath)), ManifestFile);
        }

        /// <summary>A recipe's material at a throwaway path (the slot's own; the project's stays untouched).</summary>
        static Material Recipe(string recipe)
        {
            Material material = CatalogImporter.EnsureMaterial(recipe, Manifest(), $"{Folder}/{recipe}.mat");
            AssetDatabase.SaveAssets();
            return material;
        }

        static Material ProjectMaterial(string recipe) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{recipe}.mat") ?? throw new FileNotFoundException($"{recipe}.mat");

        /// <summary>A small RGBA mask beside the metadata (its texels do not matter: no test renders it); a recipe made the folder.</summary>
        static Texture2D WriteMask()
        {
            var texture = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            texture.SetPixels32(Enumerable.Repeat(new Color32(230, 0, 80, 255), 32).ToArray());
            File.WriteAllBytes(FullPath(MaskPath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);
        }

        /// <summary>
        /// Writes the metadata and runs the importer's prefab step over the probe FBX with its slot on <paramref name="surface"/>,
        /// its damage mask's record naming <paramref name="channels"/> (a JSON list) when given.
        /// </summary>
        static Material Build(string variant, string visual, bool masked, Material surface, string channels = null)
        {
            string metaPath = $"{Folder}/{variant}.assetmeta.json";
            string textures = masked ? Textures.Replace("CHANNELS", channels == null ? "" : $", \"channels\": {channels}") : "";
            File.WriteAllText(FullPath(metaPath), Metadata.Replace("VARIANT", variant).Replace("VISUAL", visual).Replace("TEXTURES", textures));
            AssetDatabase.ImportAsset(metaPath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(metaPath);
            Assert.That(source, Is.Not.Null, "the metadata imported as a TextAsset");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(FixturePath), Is.Not.Null, $"{FixturePath} is missing: run tools/blender/tests/make_xsign_probe.py in Blender");

            string prefabPath = $"{Folder}/{variant}.prefab";
            CatalogImporter.EnsurePrefab(prefabPath, variant, Json.Obj(Json.Parse(source.text), metaPath), source, FixturePath,
                new Dictionary<string, Material> { ["probe"] = surface });
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponentInChildren<MeshRenderer>().sharedMaterial;
        }

        // --- the shader's coverage rule (Shaders/LayeredSurface's header), read from a material ------------------

        static float Coverage(Material m, int k, Vector4 mask)
        {
            Vector4 channel = m.GetVector($"_Layer{k}Channel"), coverage = m.GetVector($"_Layer{k}Coverage");
            return Mathf.Clamp01((Vector4.Dot(mask, channel) - coverage.x) / coverage.y + 0.5f);
        }

        /// <summary>The share of the surface each of the base (0) and the layers (1-5) shows under a mask value: each layer covers what lies below it.</summary>
        static float[] Shares(Material m, Vector4 mask)
        {
            var shares = new float[CatalogImporter.MaxLayers + 1];
            shares[0] = 1f;
            for (int k = 1; k <= CatalogImporter.MaxLayers; k++)
            {
                float w = Coverage(m, k, mask);
                for (int j = 0; j < k; j++)
                    shares[j] *= 1f - w;
                shares[k] = w;
            }
            return shares;
        }

        /// <summary>That a mask value shows surface <paramref name="shown"/> (0 the base, k layer k) alone.</summary>
        static void AssertShows(Material m, Vector4 mask, int shown, string what)
        {
            float[] shares = Shares(m, mask);
            for (int i = 0; i < shares.Length; i++)
                Assert.That(shares[i], Is.EqualTo(i == shown ? 1f : 0f).Within(1e-6f), $"{what}: share of {(i == 0 ? "the base" : $"layer {i}")}");
        }

        /// <summary>That layer k of the layered material is the given recipe material's surface.</summary>
        static void AssertLayerIs(Material layered, int k, Material recipe)
        {
            string prefix = $"_Layer{k}";
            Assert.That(layered.GetTexture(prefix + "Map"), Is.Not.Null.And.EqualTo(recipe.GetTexture("_BaseMap")), $"{prefix}: {recipe.name}'s albedo");
            Assert.That(layered.GetTextureScale(prefix + "Map"), Is.EqualTo(recipe.GetTextureScale("_BaseMap")), $"{prefix}: {recipe.name}'s tiling");
            AssertSameColour(layered.GetColor(prefix + "Color"), recipe.GetColor("_BaseColor"), $"{prefix}: {recipe.name}'s tint");
            Assert.That(layered.GetTexture(prefix + "BumpMap"), Is.EqualTo(recipe.GetTexture("_BumpMap")), $"{prefix}: {recipe.name}'s normal map");
            Assert.That(layered.GetTexture(prefix + "MaskMap"), Is.EqualTo(recipe.GetTexture("_MetallicGlossMap")), $"{prefix}: {recipe.name}'s packed mask");
            bool masked = recipe.IsKeywordEnabled("_METALLICSPECGLOSSMAP");
            Vector4 surface = layered.GetVector(prefix + "Surface");
            Assert.That(surface.x, Is.EqualTo(masked ? 1f : recipe.GetFloat("_Metallic")), $"{prefix}: metallic scale");
            Assert.That(surface.y, Is.EqualTo(recipe.GetFloat("_Smoothness")), $"{prefix}: smoothness scale");
        }

        /// <summary>That the layered material's base is the given recipe material's surface.</summary>
        static void AssertBaseIs(Material layered, Material recipe)
        {
            Assert.That(layered.mainTexture, Is.Not.Null.And.EqualTo(recipe.mainTexture), $"the base: {recipe.name}'s albedo");
            Assert.That(layered.GetTextureScale("_BaseMap"), Is.EqualTo(recipe.GetTextureScale("_BaseMap")), $"the base: {recipe.name}'s tiling");
            AssertSameColour(layered.GetColor("_BaseColor"), recipe.GetColor("_BaseColor"), $"the base: {recipe.name}'s tint");
            Assert.That(layered.GetTexture("_BumpMap"), Is.Not.Null.And.EqualTo(recipe.GetTexture("_BumpMap")), $"the base: {recipe.name}'s normal map");
            Assert.That(layered.GetTexture("_MetallicGlossMap"), Is.Not.Null.And.EqualTo(recipe.GetTexture("_MetallicGlossMap")), $"the base: {recipe.name}'s packed mask");
        }

        /// <summary>
        /// Notes the project's recipe materials among <paramref name="recipes"/> that do not exist yet, which a layered build
        /// creates in place: the fixture deletes them when it is done, so the tree stays as it was (H01's build commits its own).
        /// </summary>
        static void NoteNotYetBuilt(IEnumerable<string> recipes) =>
            MadeInProject.UnionWith(recipes.Select(r => $"{MaterialsFolder}/{r}.mat").Where(path => AssetDatabase.LoadAssetAtPath<Material>(path) == null));

        /// <summary>A material's colours pass through a colour-space round trip when set, so a copy matches within 1e-5.</summary>
        static void AssertSameColour(Color actual, Color expected, string what) =>
            Assert.That(new[] { actual.r, actual.g, actual.b, actual.a }, Is.EqualTo(new[] { expected.r, expected.g, expected.b, expected.a }).Within(1e-5f), what);

        static Vector4 OneHot(int channel, float value)
        {
            var v = Vector4.zero;
            v[channel] = value;
            return v;
        }

        [Test]
        public void Shader_CompilesWithURPLitsPasses()
        {
            Shader shader = Shader.Find(CatalogImporter.LayeredSurfaceShader);
            Assert.That(shader, Is.Not.Null, "Shaders/LayeredSurface.shader");
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "the shader compiles");
            // The declared subshader (the fallback's follow it), not the active one: batchmode without a GPU falls back.
            Assert.That(shader.FindSubshaderTagValue(0, new ShaderTagId("RenderPipeline")).name, Is.EqualTo("UniversalPipeline"));
            Assert.That(shader.FindSubshaderTagValue(0, new ShaderTagId("Queue")).name, Is.EqualTo("Geometry"));
            // Unity reports a built-in light mode in capitals (ShadowCaster as SHADOWCASTER).
            string[] modes = Enumerable.Range(0, shader.GetPassCountInSubshader(0)).Select(p => shader.FindPassTagValue(0, p, new ShaderTagId("LightMode")).name).ToArray();
            Assert.That(modes, Is.EqualTo(new[] { "UniversalForward", "ShadowCaster", "DepthOnly", "DepthNormals" }).IgnoreCase);
            foreach (string property in new[] { "_DamageMask", "_Layer1Map", "_Layer5Map", "_Layer5Channel", "_Layer5Coverage", "_Layer5Surface" })
                Assert.That(shader.FindPropertyIndex(property), Is.GreaterThanOrEqualTo(0), property);
        }

        [Test]
        public void CarPaint_EachChannelAtOnePicksItsLayer_AndAtZeroThePaint()
        {
            Material paint = Recipe("car_paint");
            Texture2D mask = WriteMask();

            Material layered = Build("paint", "car_paint", true, paint);

            Assert.That(AssetDatabase.GetAssetPath(layered), Is.EqualTo($"{Folder}/paint_probe_layered.mat"), "its own material, beside the prefab");
            Assert.That(layered.shader.name, Is.EqualTo(CatalogImporter.LayeredSurfaceShader));
            Assert.That(layered.renderQueue, Is.EqualTo((int)RenderQueue.Geometry), "opaque");
            Assert.That(layered.mainTexture, Is.Not.Null.And.EqualTo(paint.mainTexture), "the paint's albedo (the review refuses a material without one)");
            Assert.That(layered.GetTextureScale("_BaseMap"), Is.EqualTo(paint.GetTextureScale("_BaseMap")), "the paint's tiling");
            AssertSameColour(layered.GetColor("_BaseColor"), paint.GetColor("_BaseColor"), "the paint's tint");
            Assert.That(layered.GetTexture("_BumpMap"), Is.EqualTo(paint.GetTexture("_BumpMap")));
            Assert.That(layered.GetTexture("_MetallicGlossMap"), Is.Not.Null.And.EqualTo(paint.GetTexture("_MetallicGlossMap")));
            Assert.That(layered.GetFloat("_Metallic"), Is.EqualTo(1f), "the packed mask's metallic, unscaled");
            Assert.That(layered.GetFloat("_Smoothness"), Is.EqualTo(1f), "the packed mask's smoothness, unscaled");

            Assert.That(layered.GetTexture("_DamageMask"), Is.EqualTo(mask), "it reads its damage mask");
            Vector2 scale = layered.GetTextureScale("_DamageMask"), offset = layered.GetTextureOffset("_DamageMask");
            Assert.That(new[] { scale.x, scale.y, offset.x, offset.y }, Is.EqualTo(new[] { MaskScale.x, MaskScale.y, MaskOffset.x, MaskOffset.y }).Within(1e-5f),
                "the mask over the slot's uv_min to uv_max");
            var importer = (TextureImporter)AssetImporter.GetAtPath(MaskPath);
            Assert.That(importer.sRGBTexture, Is.False, "the mask is linear data");
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), "no damage outside the mask");

            AssertShows(layered, Vector4.zero, 0, "every channel at 0");
            for (int k = 1; k <= PaintLayers.Length; k++)
            {
                (int channel, string recipe) = PaintLayers[k - 1];
                AssertLayerIs(layered, k, ProjectMaterial(recipe));
                AssertShows(layered, OneHot(channel, 1f), k, $"{ChannelNames[channel]} at 1 ({recipe})");
                AssertShows(layered, OneHot(channel, 0f), 0, $"{ChannelNames[channel]} at 0");
                Assert.That(Coverage(layered, k, OneHot(channel, 0.9f)), Is.EqualTo(0.9f).Within(1e-6f), $"{recipe}: a channel value is its coverage");
            }
            Assert.That(Shares(layered, new Vector4(1f, 1f, 1f, 1f))[PaintLayers.Length], Is.EqualTo(1f), "the dust, last, covers everything below it");
            for (int k = PaintLayers.Length + 1; k <= CatalogImporter.MaxLayers; k++)
            {
                Assert.That(layered.GetTexture($"_Layer{k}Map"), Is.Null, $"layer {k} unused");
                Assert.That(Coverage(layered, k, new Vector4(1f, 1f, 1f, 1f)), Is.EqualTo(0f), $"layer {k} covers nothing");
            }
        }

        [TestCase("facade_h01", "whitewash")]
        [TestCase("interior_h01", "whitewash_blue")]
        public void Facade_RedAtZeroHalfAndOnePicksWhitewashRenderAndAdobe_TheBandLiesOverTheRenderAndUnderTheAdobe(string recipe, string whitewash)
        {
            // H01's facade (its design decision 2) on its own recipes and sets, through the importer's prefab step: over
            // the whitewash base, render by R (0.25, feathered 0.1), the base band by A, adobe by R (0.75), soot G, dirt B.
            string variant = recipe.Split('_')[0];
            NoteNotYetBuilt(FacadeLayers.Prepend(whitewash));
            Material fallback = Recipe(recipe);
            Texture2D mask = WriteMask();

            Assert.That(Build($"{variant}plain", recipe, false, fallback), Is.EqualTo(fallback), "an unmasked slot keeps its recipe's material");
            Assert.That(AssetDatabase.GetAssetPath(fallback.mainTexture), Is.EqualTo(AdobeDiffuse), "the bare adobe, until the house has its mask");

            Material facade = Build(variant, recipe, true, fallback, FacadeChannels);

            Assert.That(facade.shader.name, Is.EqualTo(CatalogImporter.LayeredSurfaceShader));
            Assert.That(facade.GetTexture("_DamageMask"), Is.EqualTo(mask), "it reads its damage mask");
            AssertBaseIs(facade, ProjectMaterial(whitewash));
            for (int k = 1; k <= FacadeLayers.Length; k++)
                AssertLayerIs(facade, k, ProjectMaterial(FacadeLayers[k - 1]));

            AssertShows(facade, new Vector4(0f, 0f, 0f, 0f), 0, "R 0: whitewash");
            AssertShows(facade, new Vector4(0.5f, 0f, 0f, 0f), Render, "R 0.5: render");
            AssertShows(facade, new Vector4(1f, 0f, 0f, 0f), Adobe, "R 1: adobe");
            Assert.That(Shares(facade, new Vector4(0.2f, 0f, 0f, 0f))[0], Is.EqualTo(1f).Within(1e-6f), "R 0.2: the feather's foot, all whitewash");
            Assert.That(Shares(facade, new Vector4(0.25f, 0f, 0f, 0f))[Render], Is.EqualTo(0.5f).Within(1e-6f), "R 0.25: half render, feathered");
            Assert.That(Shares(facade, new Vector4(0.3f, 0f, 0f, 0f))[Render], Is.EqualTo(1f).Within(1e-6f), "R 0.3: the feather's top, all render");
            Assert.That(Shares(facade, new Vector4(0.75f, 0f, 0f, 0f))[Adobe], Is.EqualTo(0.5f).Within(1e-6f), "R 0.75: half adobe, feathered");

            AssertShows(facade, new Vector4(0f, 0f, 0f, 1f), Band, "A 1 over the whitewash: the band");
            AssertShows(facade, new Vector4(0.5f, 0f, 0f, 1f), Band, "A 1 and R 0.5: the band, paint over the render");
            AssertShows(facade, new Vector4(1f, 0f, 0f, 1f), Adobe, "A 1 and R 1: the bare adobe, under no paint");
            Assert.That(Shares(facade, new Vector4(0.5f, 0f, 0f, 0.5f))[Band], Is.EqualTo(0.5f).Within(1e-6f), "A 0.5: half band");

            AssertShows(facade, new Vector4(0.5f, 1f, 0f, 1f), Soot, "G 1: soot over the band and the render");
            AssertShows(facade, new Vector4(1f, 0f, 1f, 0f), Dirt, "B 1: dirt over the adobe");
            Assert.That(Shares(facade, new Vector4(0f, 0.85f, 0f, 0f))[Soot], Is.EqualTo(0.85f).Within(1e-6f), "a channel value is its coverage");
        }

        [Test]
        public void MaskChannelsThatAreNotTheRecipes_AreRefused()
        {
            Material facade = Recipe("facade_h01");
            Material paint = Recipe("car_paint");
            WriteMask();

            var renamed = Assert.Throws<FormatException>(() => Build("misnamed", "facade_h01", true, facade, @"[""render_depth"", ""soot"", ""dust"", ""base_band""]"));
            Assert.That(renamed.Message, Does.Contain("the mask's channels (R, G, B, A) are render_depth, soot, dust, base_band, but its recipe facade_h01's layers read render_depth, soot, dirt, base_band"));
            var shortened = Assert.Throws<FormatException>(() => Build("shortened", "facade_h01", true, facade, @"[""render_depth"", ""soot"", ""dirt""]"));
            Assert.That(shortened.Message, Does.Contain("are render_depth, soot, dirt, but"), "a mask without the band's channel");
            var unnamed = Assert.Throws<FormatException>(() => Build("carnamed", "car_paint", true, paint, @"[""rust"", ""soot"", ""dust"", ""bare""]"));
            Assert.That(unnamed.Message, Does.Contain("but its recipe car_paint names none to hold them to"));
        }

        [Test]
        public void UnmaskedSlot_KeepsItsRecipesMaterial_AndASecondBuildChangesNoFile()
        {
            // Its own variant name: an asset path another test deleted is not reused within the session.
            Material paint = Recipe("car_paint");
            WriteMask();

            Assert.That(Build("plain", "car_paint", false, paint), Is.EqualTo(paint), "a slot without a mask draws with its recipe's material");

            Build("repaint", "car_paint", true, paint);
            Dictionary<string, byte[]> first = Snapshot();
            Assert.That(first.Keys, Does.Contain("repaint_probe_layered.mat"));
            Build("repaint", "car_paint", true, paint);
            Dictionary<string, byte[]> second = Snapshot();
            Assert.That(second.Keys, Is.EquivalentTo(first.Keys), "the same files");
            foreach (KeyValuePair<string, byte[]> file in first)
                Assert.That(second[file.Key], Is.EqualTo(file.Value), $"{file.Key} changed");

            Assert.That(Build("repaint", "car_paint", false, paint), Is.EqualTo(paint), "unmasked again: the recipe's material");
            Assert.That(Snapshot().Keys, Does.Not.Contain("repaint_probe_layered.mat"), "the masked build's layered material is gone");
        }

        [Test]
        public void MalformedLayers_AreRefused()
        {
            Material paint = Recipe("car_paint");
            Texture2D mask = WriteMask();

            var noLayers = Assert.Throws<FormatException>(() => Build("plainpaint", "wire_sip", true, Recipe("wire_sip")));
            Assert.That(noLayers.Message, Does.Contain("lists no \"layers\""));

            Material Layered(string json) => CatalogImporter.EnsureLayeredMaterial(paint, Json.Arr(Json.Parse(json), "layers"), Manifest(), mask,
                Vector2.zero, Vector2.one, $"{Folder}/bad_layered.mat", "bad");
            Assert.That(Assert.Throws<FormatException>(() => Layered(@"[ { ""channel"": ""x"", ""recipe"": ""car_rust"" } ]")).Message, Does.Contain("\"channel\""));
            Assert.That(Assert.Throws<FormatException>(() => Layered(@"[ { ""channel"": ""r"", ""recipe"": ""car_rust"", ""feather"": 0 } ]")).Message, Does.Contain("\"feather\""));
            Assert.That(Assert.Throws<FormatException>(() => Layered(@"[ { ""channel"": ""r"", ""recipe"": ""car_rust"", ""treshold"": 0.3 } ]")).Message, Does.Contain("unknown key \"treshold\""));
            Assert.That(Assert.Throws<FormatException>(() => Layered(@"[ { ""channel"": ""r"", ""recipe"": ""glass"" } ]")).Message, Does.Contain("not an opaque"));
            Assert.That(Assert.Throws<FormatException>(() => Layered("[]")).Message, Does.Contain("0 layers"));
            string six = "[" + string.Join(",", Enumerable.Repeat(@"{ ""channel"": ""r"", ""recipe"": ""car_rust"" }", 6)) + "]";
            Assert.That(Assert.Throws<FormatException>(() => Layered(six)).Message, Does.Contain("6 layers"));
        }

        static Dictionary<string, byte[]> Snapshot() =>
            Directory.GetFiles(FullPath(Folder)).ToDictionary(file => Path.GetFileName(file), file => File.ReadAllBytes(file));

        static string FullPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }
}
