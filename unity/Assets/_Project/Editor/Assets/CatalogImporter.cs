using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Fpv450.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Fpv450.Assets
{
    /// <summary>
    /// Catalog plus metadata to prefab (setup-asset-pipeline, design decision 8). For every variant of an asset id:
    /// the URP materials from <c>assets-src/materials/&lt;visual&gt;.json</c>, assigned by the FBX's slot names; the
    /// prefab under <c>Prefabs/&lt;class&gt;/&lt;variant&gt;.prefab</c> with a LODGroup over the FBX's LOD meshes,
    /// colliders from the metadata primitives and an <see cref="AssetPhysicsMeta"/> filled from the
    /// <c>.assetmeta.json</c>. Idempotent: with unchanged inputs a second run changes no file.
    /// Batchmode: <c>-executeMethod Fpv450.Assets.CatalogImporter.ImportFromCommandLine -asset &lt;ID|all&gt;</c>,
    /// exit code 1 on any error.
    /// </summary>
    public static class CatalogImporter
    {
        const string CatalogFile = "assets-src/catalog.json";
        const string ManifestFile = "tools/assets/manifest.json";
        const string RecipesFolder = "assets-src/materials";
        const string ModelsFolder = "Assets/_Project/Art/Models";
        const string MaterialsFolder = "Assets/_Project/Art/Materials";
        const string PrefabsFolder = "Assets/_Project/Prefabs";
        const string LitShader = "Universal Render Pipeline/Lit";
        const string LeafShader = "Fpv450/Leaf";

        // The metadata gives LOD distances; a LODGroup wants screen-relative heights. The conversion assumes the
        // feed's vertical field of view (design decision 7, derived: 60 degrees, 935 px/rad at 1080 px).
        const float VerticalFovDeg = 60f;

        // A cylinder collider is a closed right prism over a regular N-gon inscribed in the circle, so its surface
        // sits inside the true one by the chord's sagitta, r·(1 − cos(π/N)). N is the smallest count that keeps
        // that within the tolerance, rounded up to a multiple of 4 so a vertex sits on ±X and ±Z and the bounds
        // are exactly ±r; at least 8, and at most 64 because a PhysX convex hull takes 255 vertices at most.
        const float CylinderToleranceM = 0.005f;
        const int CylinderMinSegments = 8;
        const int CylinderMaxSegments = 64;

        public static void ImportFromCommandLine()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-asset");
                if (at < 0 || at + 1 >= args.Length)
                    throw new ArgumentException("usage: -executeMethod Fpv450.Assets.CatalogImporter.ImportFromCommandLine -asset <ID|all>");
                string id = args[at + 1];
                AssetDatabase.Refresh();
                if (id == "all")
                    foreach (string each in Catalog().Keys.OrderBy(k => k, StringComparer.Ordinal))
                        Import(each);
                else
                    Import(id);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CatalogImporter] FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
        }

        public static void Import(string id)
        {
            Dictionary<string, object> catalog = Catalog();
            if (!catalog.TryGetValue(id, out object entryValue))
                throw new ArgumentException($"{id} is not an asset of {CatalogFile}");
            Dictionary<string, object> entry = Json.Obj(entryValue, $"{CatalogFile} {id}");
            string assetClass = Json.Str(entry, "class", $"{CatalogFile} {id}");
            Dictionary<string, object> manifest = Json.Obj(Json.Parse(ReadRepoFile(ManifestFile)), ManifestFile);

            foreach (object variantValue in Json.Arr(entry, "variants", $"{CatalogFile} {id}"))
            {
                string variant = variantValue as string ?? throw new FormatException($"{CatalogFile} {id}: variants must be strings");
                string fbxPath = $"{ModelsFolder}/{assetClass}/{id}/{variant}.fbx";
                string metaPath = $"{ModelsFolder}/{assetClass}/{id}/{variant}.assetmeta.json";
                foreach (string path in new[] { fbxPath, metaPath })
                    if (!File.Exists(FullPath(path)))
                        throw new FileNotFoundException($"{path} is missing: run the Blender build first");

                EnsureModelImport(fbxPath);
                var metaJson = Json.Obj(Json.Parse(File.ReadAllText(FullPath(metaPath))), metaPath);
                var materialBySlot = new Dictionary<string, Material>();
                foreach (object slotValue in Json.Arr(metaJson, "materials", metaPath))
                {
                    var slot = Json.Obj(slotValue, $"{metaPath} materials");
                    materialBySlot[Json.Str(slot, "slot", metaPath)] = EnsureMaterial(Json.Str(slot, "visual", metaPath), manifest);
                }

                string prefabPath = $"{PrefabsFolder}/{assetClass}/{variant}.prefab";
                EnsureFolder($"{PrefabsFolder}/{assetClass}");
                EnsurePrefab(prefabPath, variant, metaJson, AssetDatabase.LoadAssetAtPath<TextAsset>(metaPath), fbxPath, materialBySlot);
            }
            AssetDatabase.SaveAssets();
        }

        // --- the catalog and the repo ---------------------------------------------------------------------------

        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        static string FullPath(string assetPath) => Path.Combine(RepoRoot, "unity", assetPath);

        static string ReadRepoFile(string relative)
        {
            string path = Path.Combine(RepoRoot, relative);
            if (!File.Exists(path))
                throw new FileNotFoundException($"{relative} is missing");
            return File.ReadAllText(path);
        }

        static Dictionary<string, object> Catalog()
        {
            var catalog = Json.Obj(Json.Parse(ReadRepoFile(CatalogFile)), CatalogFile);
            if ((int)Json.Num(catalog, "schema", CatalogFile) != 1)
                throw new NotSupportedException($"{CatalogFile}: schema {catalog["schema"]}; this importer reads schema 1");
            return Json.Obj(catalog, "assets", CatalogFile);
        }

        static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;
            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder))))
                throw new IOException($"could not create {assetFolder}");
        }

        // --- import settings --------------------------------------------------------------------------------------

        static void EnsureModelImport(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter
                ?? throw new FileNotFoundException($"{fbxPath} is not in the asset database");
            if (ArtImportSettings.ApplyModel(importer))
                importer.SaveAndReimport();
        }

        static void EnsureTextureImport(string texturePath, string hint)
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter
                ?? throw new FileNotFoundException($"{texturePath} is not in the asset database: {hint}");
            if (ArtImportSettings.ApplyTexture(importer, texturePath))
                importer.SaveAndReimport();
        }

        // --- materials ------------------------------------------------------------------------------------------

        /// <summary>
        /// The material of one recipe, created or updated in place. URP/Lit gets the set's diffuse as base map,
        /// tiled by the set's real-world size (the UVs are in metres, AST-07), its normal map and the tint. With a
        /// "mask" in the recipe, the packed mask that <c>tools/assets/derive.py</c> derives from it (metallic R,
        /// occlusion G, smoothness A) is read twice, as <c>_MetallicGlossMap</c> with the albedo-alpha smoothness
        /// option off and <c>_Smoothness</c> at 1 so the alpha is not scaled, and as <c>_OcclusionMap</c>; without
        /// one, smoothness is the recipe's constant. The roughness and AO sources are never assigned themselves:
        /// URP/Lit reads those channels only from packed maps. An "alpha_clip" (a cutoff in 0–1, p1-v13-shrub)
        /// turns on URP/Lit's alpha clipping at that cutoff on an opaque surface, read from the base map's alpha:
        /// the diffuse's own, or, when the set ships its opacity as a separate map, the <c>_base</c> map that
        /// <c>derive.py</c> packs from both. "two_sided" renders both faces (cull off). A recipe that is both is a
        /// leaf (p1-v13-shrub, design decision 6): it gets <c>Shaders/Leaf</c>, which reads the same properties and
        /// lights a back face with its own normal, unless its "shader" names another than URP/Lit; and its base map
        /// keeps its alpha coverage at the cutoff in every mip.
        /// <para>
        /// A recipe of kind <c>ground</c> (p1-s04-belt-straw) takes the maps <c>derive.py</c> composes for it as its
        /// base: <c>&lt;visual&gt;_albedo.png</c>, <c>_normal.png</c> and <c>_mask.png</c>, which span
        /// <c>size_m</c> × <c>size_m</c> centred on the asset origin, so the base tiling is 1/size with a half-tile
        /// offset. The detail layer is the derived <c>_detail_albedo.png</c> (the detail set's diffuse, neutralised
        /// to mid-grey for URP's "detail albedo ×2") and the detail set's own normal map, keyword
        /// <c>_DETAIL_MULX2</c>. URP/Lit derives the detail UV from the base UV times the detail tiling, so the
        /// tiling is size/tile_size_m: the detail repeats every tile_size_m metres of the asset. A ground recipe in
        /// wall space (<c>"space": "wall"</c>, p1-e01-trench) is a cut's strip: its <c>size_m</c> is [u, v] metres
        /// from the strip's corner (the generator UVs the cut's faces in metres along the run and up the strip), so
        /// the base tiling is 1/u by 1/v with no offset and the detail tiling u/tile by v/tile.
        /// </para>
        /// <para>
        /// A recipe of kind <c>palette</c> (the straw instance kit's tints) has no texture set: its base map is the
        /// flat <c>&lt;visual&gt;_albedo.png</c> that <c>derive.py</c> writes from its colours, one block per colour,
        /// at tiling 1 (a generator UVs each face into its column), with the recipe's constant smoothness and no
        /// normal or mask.
        /// </para>
        /// <para>
        /// A transparent recipe (<c>"blend": "transparent"</c>, p1-r01-vaz-sedan design decision 6) is a palette drawn
        /// with <c>Shaders/CrazedGlass</c>: the palette's albedo times the tint, whose alpha is the share of the
        /// background each face of the pane stops, its constant smoothness and its index of refraction (<c>"ior"</c>), in the
        /// transparent queue. That material is the plain glass of every intact pane; a crazed pane draws with its own
        /// (<see cref="EnsureCrazedPaneMaterial"/>).
        /// </para>
        /// <para>
        /// A recipe may list <c>"layers"</c> (p1-r01-vaz-sedan design decision 5): what a damage mask lays over it, with
        /// an optional <c>"base"</c> recipe they lie over and the optional <c>"channels"</c> they read (p1-h01-adobe-house
        /// design decision 2). Its own material ignores them and stays URP/Lit; a slot whose metadata gives it a damage
        /// mask draws with its own layered material (<see cref="EnsureLayeredMaterial"/>).
        /// </para>
        /// Internal for the EditMode tests, which build the belt_straw material without an asset around it.
        /// </summary>
        internal static Material EnsureMaterial(string visual, Dictionary<string, object> manifest) =>
            EnsureMaterial(visual, manifest, $"{MaterialsFolder}/{visual}.mat");

        /// <summary>The material of one recipe at an explicit path. Internal for the EditMode tests, which build one at a throwaway path.</summary>
        internal static Material EnsureMaterial(string visual, Dictionary<string, object> manifest, string materialPath)
        {
            string recipePath = $"{RecipesFolder}/{visual}.json";
            var recipe = Json.Obj(Json.Parse(ReadRepoFile(recipePath)), recipePath);
            if ((int)Json.Num(recipe, "schema", recipePath) != 1)
                throw new NotSupportedException($"{recipePath}: schema {recipe["schema"]}; this importer reads schema 1");
            string shaderName = Json.Str(recipe, "shader", recipePath);
            string kind = recipe.TryGetValue("kind", out object kindValue)
                ? kindValue as string ?? throw new FormatException($"{recipePath}: \"kind\" must be a string")
                : null;
            if (kind != null && kind != "ground" && kind != "palette")
                throw new FormatException($"{recipePath}: unknown recipe kind \"{kind}\" (omit it for a tiled set, or \"ground\" or \"palette\")");
            string blend = recipe.TryGetValue("blend", out object blendValue)
                ? blendValue as string ?? throw new FormatException($"{recipePath}: \"blend\" must be a string")
                : null;
            if (blend != null && blend != "transparent")
                throw new FormatException($"{recipePath}: unknown blend \"{blend}\" (omit it for an opaque surface, or \"transparent\")");
            bool transparent = blend == "transparent";
            if (transparent && (kind != "palette" || shaderName != CrazedGlassShader || recipe.ContainsKey("alpha_clip") || recipe.ContainsKey("two_sided")))
                throw new FormatException($"{recipePath}: a transparent recipe is a palette drawn with {CrazedGlassShader}, which is two-sided and never clipped");
            float ior = transparent ? Json.Num(recipe, "ior", recipePath) : 0f;
            if (transparent && ior < 1f)
                throw new FormatException($"{recipePath}: \"ior\" must be an index of refraction of 1 or more, not {ior}");
            bool ground = kind == "ground";

            Texture2D albedo, normal, mask, detailAlbedo = null, detailNormal = null;
            Vector2 baseScale, baseOffset = Vector2.zero, detailScale = Vector2.one;
            bool masked, separateOpacity = false;
            if (kind == "palette")
            {
                if (recipe.ContainsKey("texture_set") || recipe.ContainsKey("mask"))
                    throw new FormatException($"{recipePath}: a palette recipe is flat colour; it takes neither \"texture_set\" nor \"mask\"");
                albedo = Derived(visual, "albedo");
                normal = null;
                mask = null;
                masked = false;
                baseScale = Vector2.one;
            }
            else if (ground)
            {
                if (recipe.ContainsKey("mask") || recipe.ContainsKey("smoothness"))
                    throw new FormatException($"{recipePath}: a ground recipe derives its own mask; it takes neither \"mask\" nor \"smoothness\"");
                string space = recipe.TryGetValue("space", out object spaceValue)
                    ? spaceValue as string ?? throw new FormatException($"{recipePath}: \"space\" must be a string")
                    : null;
                if (space != null && space != "wall")
                    throw new FormatException($"{recipePath}: unknown ground space \"{space}\" (omit it for a patch, or \"wall\")");
                bool wall = space == "wall";
                Vector2 size = wall ? Json.Vec2(recipe, "size_m", recipePath) : Vector2.one * Json.Num(recipe, "size_m", recipePath);
                var detail = Json.Obj(recipe, "detail", recipePath);
                float detailTile = Json.Num(detail, "tile_size_m", recipePath);
                if (size.x <= 0f || size.y <= 0f || detailTile <= 0f)
                    throw new FormatException($"{recipePath}: size_m and detail.tile_size_m must be positive metres");
                albedo = Derived(visual, "albedo");
                normal = Derived(visual, "normal");
                mask = Derived(visual, "mask");
                detailAlbedo = Derived(visual, "detail_albedo");
                Dictionary<string, Texture2D> detailSet = TextureSet(Json.Str(detail, "texture_set", recipePath), manifest, recipePath);
                if (!detailSet.TryGetValue("normal", out detailNormal))
                    throw new FormatException($"{recipePath}: the detail texture set has no normal map");
                baseScale = new Vector2(1f / size.x, 1f / size.y);
                baseOffset = wall ? Vector2.zero : new Vector2(0.5f, 0.5f);
                detailScale = new Vector2(size.x / detailTile, size.y / detailTile);
                masked = true;
            }
            else
            {
                Dictionary<string, Texture2D> maps = TextureSet(Json.Str(recipe, "texture_set", recipePath), manifest, recipePath);
                Vector2 tile = Json.Vec2(recipe, "tile_size_m", recipePath);
                if (!maps.TryGetValue("diffuse", out albedo))
                    throw new FormatException($"{recipePath}: the texture set has no diffuse map");
                maps.TryGetValue("normal", out normal);
                separateOpacity = maps.ContainsKey("opacity");
                masked = recipe.ContainsKey("mask");
                if (masked == recipe.ContainsKey("smoothness"))
                    throw new FormatException($"{recipePath}: give \"smoothness\" as a constant or a \"mask\" whose alpha carries it, not both or neither");
                mask = masked ? Derived(visual, "mask") : null;
                baseScale = new Vector2(1f / tile.x, 1f / tile.y);
            }
            bool clipped = recipe.ContainsKey("alpha_clip");
            float cutoff = clipped ? Json.Num(recipe, "alpha_clip", recipePath) : 0.5f;
            if (cutoff < 0f || cutoff > 1f)
                throw new FormatException($"{recipePath}: \"alpha_clip\" must be a cutoff in 0–1, not {cutoff}");
            bool twoSided = recipe.ContainsKey("two_sided") && Json.Bool(recipe, "two_sided", recipePath);
            if (clipped && twoSided && shaderName == LitShader)
                shaderName = LeafShader;
            Shader shader = Shader.Find(shaderName) ?? throw new FormatException($"{recipePath}: no shader named \"{shaderName}\"");
            if (clipped && separateOpacity)
                albedo = Derived(visual, "base");
            if (clipped)
                EnsureCoverage(albedo, cutoff);

            EnsureFolder(Path.GetDirectoryName(materialPath).Replace('\\', '/'));
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool changed = false;
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                changed = true;
            }
            changed |= SetTexture(material, "_BaseMap", albedo);
            changed |= SetTextureScale(material, "_BaseMap", baseScale);
            changed |= SetTextureOffset(material, "_BaseMap", baseOffset);
            changed |= SetColor(material, "_BaseColor", Json.Rgba(recipe, "tint", recipePath));
            if (transparent) // the plain glass: CrazedGlass reads only these, and no crack mask (a crazed pane gets its own)
            {
                changed |= SetFloat(material, "_Smoothness", Json.Num(recipe, "smoothness", recipePath));
                changed |= SetFloat(material, "_IOR", ior);
                changed |= SetQueue(material, (int)RenderQueue.Transparent);
                if (changed)
                    EditorUtility.SetDirty(material);
                return material;
            }
            changed |= SetTexture(material, "_BumpMap", normal);
            changed |= SetKeyword(material, "_NORMALMAP", normal != null);
            changed |= SetTexture(material, "_MetallicGlossMap", mask);
            changed |= SetKeyword(material, "_METALLICSPECGLOSSMAP", masked);
            changed |= SetTexture(material, "_OcclusionMap", mask);
            changed |= SetKeyword(material, "_OCCLUSIONMAP", masked);
            changed |= SetFloat(material, "_SmoothnessTextureChannel", 0f);
            changed |= SetKeyword(material, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", false);
            changed |= SetFloat(material, "_Smoothness", masked ? 1f : Json.Num(recipe, "smoothness", recipePath));
            changed |= SetFloat(material, "_Metallic", 0f);
            if (material.HasProperty("_DetailAlbedoMap")) // URP/Lit's detail layer; the leaf shader has none
            {
                changed |= SetTexture(material, "_DetailAlbedoMap", detailAlbedo);
                changed |= SetTextureScale(material, "_DetailAlbedoMap", detailScale);
                changed |= SetTexture(material, "_DetailNormalMap", detailNormal);
                changed |= SetFloat(material, "_DetailAlbedoMapScale", 1f);
                changed |= SetFloat(material, "_DetailNormalMapScale", 1f);
                changed |= SetKeyword(material, "_DETAIL_MULX2", ground);
            }
            // Opaque with clipping, as URP/Lit's own inspector sets it: the cutoff, the keyword, the alpha-test queue
            // and render type, alpha to coverage under MSAA; two-sided is cull off, with double-sided GI to match.
            changed |= SetFloat(material, "_AlphaClip", clipped ? 1f : 0f);
            changed |= SetFloat(material, "_Cutoff", cutoff);
            changed |= SetKeyword(material, "_ALPHATEST_ON", clipped);
            changed |= SetFloat(material, "_AlphaToMask", clipped ? 1f : 0f);
            changed |= SetFloat(material, "_Cull", (float)(twoSided ? CullMode.Off : CullMode.Back));
            if (material.doubleSidedGI != twoSided)
            {
                material.doubleSidedGI = twoSided;
                changed = true;
            }
            int queue = clipped ? (int)RenderQueue.AlphaTest : -1;
            if (material.renderQueue != queue)
            {
                material.renderQueue = queue;
                changed = true;
            }
            if (clipped && material.GetTag("RenderType", false) != "TransparentCutout")
            {
                material.SetOverrideTag("RenderType", "TransparentCutout");
                changed = true;
            }
            if (changed)
                EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The base map of a clipped recipe keeps its alpha coverage at the recipe's cutoff in every mip: averaged
        /// mips of a leaf atlas fall under the cutoff, and a far card thins out or vanishes (design decision 6).
        /// </summary>
        static void EnsureCoverage(Texture2D baseMap, float cutoff)
        {
            string path = AssetDatabase.GetAssetPath(baseMap);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new FileNotFoundException($"{path} is not a texture in the asset database");
            if (ArtImportSettings.ApplyCoverage(importer, cutoff))
                importer.SaveAndReimport();
        }

        /// <summary>
        /// A map derived from a recipe, <c>Art/Materials/&lt;visual&gt;_&lt;role&gt;.png</c>, imported as its role needs
        /// (<see cref="ArtImportSettings"/>): the packed mask, a ground recipe's maps, a palette's albedo, or the
        /// <c>_base</c> map of an alpha-clipped recipe whose set ships its opacity separately (the diffuse's RGB, the
        /// opacity in A, an sRGB map that keeps its alpha).
        /// </summary>
        static Texture2D Derived(string visual, string role)
        {
            string path = $"{MaterialsFolder}/{visual}_{role}.png";
            EnsureTextureImport(path, $"run python tools/assets/derive.py {visual}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                ?? throw new FileNotFoundException($"{path} did not import as a texture");
        }

        /// <summary>The imported textures of a manifest entry, by map name (diffuse, normal, roughness, ao, opacity, displacement).</summary>
        static Dictionary<string, Texture2D> TextureSet(string set, Dictionary<string, object> manifest, string what)
        {
            foreach (object entryValue in Json.Arr(manifest, "entries", ManifestFile))
            {
                var entry = Json.Obj(entryValue, ManifestFile);
                if ($"{Json.Str(entry, "source", ManifestFile)}/{Json.Str(entry, "id", ManifestFile)}" != set)
                    continue;
                string destination = Json.Str(entry, "destination", ManifestFile);
                if (!destination.StartsWith("unity/Assets/", StringComparison.Ordinal))
                    throw new FormatException($"{ManifestFile} {set}: destination {destination} is not under unity/Assets/");
                string folder = destination.Substring("unity/".Length);
                var maps = new Dictionary<string, Texture2D>();
                foreach (object fileValue in Json.Arr(entry, "files", ManifestFile))
                {
                    var file = Json.Obj(fileValue, ManifestFile);
                    string path = $"{folder}/{Json.Str(file, "name", ManifestFile)}";
                    EnsureTextureImport(path, "run python tools/assets/fetch.py");
                    maps[Json.Str(file, "map", ManifestFile)] = AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                        ?? throw new FileNotFoundException($"{path} did not import as a texture");
                }
                return maps;
            }
            throw new FormatException($"{what}: texture set \"{set}\" is not in {ManifestFile}");
        }

        static bool SetTexture(Material m, string name, Texture value)
        {
            if (m.GetTexture(name) == value && (value != null || !NamesMissingTexture(m, name)))
                return false;
            m.SetTexture(name, value);
            return true;
        }

        /// <summary>
        /// Whether the material's texture property still names an asset that is gone (a recipe that dropped its mask):
        /// such a reference reads back as null but stays in the file until it is set again.
        /// </summary>
        static bool NamesMissingTexture(Material m, string name)
        {
            SerializedProperty textures = new SerializedObject(m).FindProperty("m_SavedProperties.m_TexEnvs");
            for (int i = 0; i < textures.arraySize; i++)
            {
                SerializedProperty entry = textures.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == name)
                    return entry.FindPropertyRelative("second.m_Texture").objectReferenceInstanceIDValue != 0;
            }
            return false;
        }

        static bool SetTextureScale(Material m, string name, Vector2 value)
        {
            if (m.GetTextureScale(name) == value)
                return false;
            m.SetTextureScale(name, value);
            return true;
        }

        static bool SetTextureOffset(Material m, string name, Vector2 value)
        {
            if (m.GetTextureOffset(name) == value)
                return false;
            m.SetTextureOffset(name, value);
            return true;
        }

        static bool SetColor(Material m, string name, Color value)
        {
            if (m.GetColor(name) == value)
                return false;
            m.SetColor(name, value);
            return true;
        }

        static bool SetFloat(Material m, string name, float value)
        {
            if (m.GetFloat(name) == value)
                return false;
            m.SetFloat(name, value);
            return true;
        }

        static bool SetKeyword(Material m, string keyword, bool on)
        {
            if (m.IsKeywordEnabled(keyword) == on)
                return false;
            if (on)
                m.EnableKeyword(keyword);
            else
                m.DisableKeyword(keyword);
            return true;
        }

        static bool SetQueue(Material m, int queue)
        {
            if (m.renderQueue == queue)
                return false;
            m.renderQueue = queue;
            return true;
        }

        // --- thin wires (p1-l01-l03-pole-wires, design decision 8; tech-artist) -----------------------------------

        internal const string ThinWireShader = "Fpv450/ThinWire";
        internal const string ThinWireKeyword = "_THIN_WIRE_COVERAGE";

        /// <summary>
        /// The material of a wire tube whose slot maps to <paramref name="surface"/>, the recipe's own material from
        /// <see cref="EnsureMaterial(string, Dictionary{string, object})"/>: <c>Shaders/ThinWire</c> with
        /// <see cref="ThinWireKeyword"/> on and the recipe's surface (base map and its tiling, tint, smoothness or the
        /// packed mask, occlusion), at <c>&lt;surface&gt;_thin_wire.mat</c> beside it, created or updated in place. The
        /// recipe's own material stays URP/Lit, so one recipe still draws both a span's conductor and a pole's
        /// grounding wire. The tube's mesh must carry its radius in uv2 and radial normals (the shader's header).
        /// </summary>
        internal static Material EnsureThinWireMaterial(Material surface)
        {
            string surfacePath = AssetDatabase.GetAssetPath(surface);
            if (string.IsNullOrEmpty(surfacePath))
                throw new ArgumentException($"{surface.name} is not a material asset");
            Shader shader = Shader.Find(ThinWireShader) ?? throw new FileNotFoundException($"no shader named \"{ThinWireShader}\"");
            string path = $"{Path.GetDirectoryName(surfacePath).Replace('\\', '/')}/{surface.name}_thin_wire.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool changed = false;
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                changed = true;
            }
            foreach (string texture in new[] { "_BaseMap", "_MetallicGlossMap", "_OcclusionMap" })
                changed |= SetTexture(material, texture, surface.GetTexture(texture));
            changed |= SetTextureScale(material, "_BaseMap", surface.GetTextureScale("_BaseMap"));
            changed |= SetTextureOffset(material, "_BaseMap", surface.GetTextureOffset("_BaseMap"));
            changed |= SetColor(material, "_BaseColor", surface.GetColor("_BaseColor"));
            foreach (string value in new[] { "_Smoothness", "_Metallic", "_OcclusionStrength" })
                changed |= SetFloat(material, value, surface.GetFloat(value));
            foreach (string keyword in new[] { "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP" })
                changed |= SetKeyword(material, keyword, surface.IsKeywordEnabled(keyword));
            changed |= SetFloat(material, "_ThinWireCoverage", 1f);
            changed |= SetKeyword(material, ThinWireKeyword, true);
            if (material.renderQueue != (int)RenderQueue.Transparent) // the shader's own, set so it holds without a GPU too
            {
                material.renderQueue = (int)RenderQueue.Transparent;
                changed = true;
            }
            if (changed)
                EditorUtility.SetDirty(material);
            return material;
        }

        // --- crazed panes (p1-r01-vaz-sedan, design decision 6; tech-artist) ----------------------------------------

        internal const string CrazedGlassShader = "Fpv450/CrazedGlass";
        internal const string CrackMaskRole = "crack_mask";
        internal const string DamageMaskRole = "damage_mask";

        /// <summary>
        /// The slot materials with each crazed pane's and each layered surface's own. The metadata's optional
        /// <c>textures</c> lists the textures a generator wrote beside it, each naming a <c>slot</c> (its faces in the
        /// FBX), the mask's <c>file</c> beside the metadata, and where the mask lies in that slot's UVs, which stay the
        /// asset's metres (AST-07): <c>uv_min</c> and <c>uv_max</c>, its lower-left and upper-right corners; every mask is
        /// imported as linear, clamped data. A slot with a <c>crack_mask</c> draws with its own pane material,
        /// <c>&lt;variant&gt;_&lt;slot&gt;_crazed.mat</c> beside the prefab (<see cref="EnsureCrazedPaneMaterial"/>). A slot with
        /// a <c>damage_mask</c> (p1-r01-vaz-sedan task 1.2: the vehicle's damage mask over its paint's UV canvas; a
        /// house's over its walls, p1-h01-adobe-house) draws with its own layered material,
        /// <c>&lt;variant&gt;_&lt;slot&gt;_layered.mat</c> beside the prefab, its recipe's surface (or its <c>"base"</c>
        /// recipe's) under the layers the recipe lists (<see cref="EnsureLayeredMaterial"/>, <see cref="LayeredRecipe"/>).
        /// Every other slot keeps its recipe's material, so an intact pane draws with the plain glass, an unmasked paint
        /// with the plain paint and an unmasked facade with its bare adobe, and a pane or layered material left from a
        /// build in which that slot had its mask is deleted. Refused: another role, a slot without a material in the
        /// metadata, two masks of one role on one slot, a crack mask on a slot whose recipe is not transparent, a damage
        /// mask on a slot whose recipe lists no layers, and a damage mask whose channels are not its recipe's.
        /// </summary>
        static Dictionary<string, Material> WithMaskedSlots(string prefabPath, string variant, Dictionary<string, object> metaJson,
            string metaPath, Dictionary<string, Material> materialBySlot)
        {
            var crackMasks = new Dictionary<string, Dictionary<string, object>>();
            var damageMasks = new Dictionary<string, Dictionary<string, object>>();
            string folder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            string metaFolder = Path.GetDirectoryName(metaPath).Replace('\\', '/');
            if (metaJson.TryGetValue("textures", out object texturesValue))
                foreach (object item in Json.Arr(texturesValue, $"{metaPath} \"textures\""))
                {
                    var entry = Json.Obj(item, $"{metaPath} textures");
                    string role = Json.Str(entry, "role", $"{metaPath} textures");
                    if (role != CrackMaskRole && role != DamageMaskRole)
                        throw new FormatException($"{metaPath}: unknown texture role \"{role}\" (the importer knows \"{CrackMaskRole}\" and \"{DamageMaskRole}\")");
                    string slot = Json.Str(entry, "slot", $"{metaPath} textures");
                    if (!materialBySlot.ContainsKey(slot))
                        throw new FormatException($"{metaPath}: a {role} names slot \"{slot}\", which has no material in the metadata");
                    if (!(role == CrackMaskRole ? crackMasks : damageMasks).TryAdd(slot, entry))
                        throw new FormatException($"{metaPath}: two {(role == CrackMaskRole ? "crack" : "damage")} masks on slot \"{slot}\"");
                }

            Dictionary<string, object> manifest = damageMasks.Count == 0 ? null : Json.Obj(Json.Parse(ReadRepoFile(ManifestFile)), ManifestFile);
            var result = new Dictionary<string, Material>();
            foreach (KeyValuePair<string, Material> slot in materialBySlot)
            {
                string panePath = $"{folder}/{variant}_{slot.Key}_crazed.mat";
                string layeredPath = $"{folder}/{variant}_{slot.Key}_layered.mat";
                bool crazed = crackMasks.TryGetValue(slot.Key, out Dictionary<string, object> crack);
                bool layered = damageMasks.TryGetValue(slot.Key, out Dictionary<string, object> damage);
                if (crazed)
                {
                    string what = $"{metaPath} crack mask of slot {slot.Key}";
                    result[slot.Key] = EnsureCrazedPaneMaterial(slot.Value, CrackMask($"{metaFolder}/{Json.Str(crack, "file", what)}"),
                        Json.Vec2(crack, "uv_min", what), Json.Vec2(crack, "uv_max", what), panePath, what);
                }
                else if (layered)
                {
                    string what = $"{metaPath} damage mask of slot {slot.Key}";
                    (List<object> layers, string baseRecipe) = LayeredRecipe(VisualOf(metaJson, slot.Key, metaPath), damage, what);
                    Material surface = baseRecipe == null ? slot.Value : EnsureMaterial(baseRecipe, manifest);
                    result[slot.Key] = EnsureLayeredMaterial(surface, layers, manifest,
                        CrackMask($"{metaFolder}/{Json.Str(damage, "file", what)}"), Json.Vec2(damage, "uv_min", what), Json.Vec2(damage, "uv_max", what),
                        layeredPath, what);
                }
                else
                    result[slot.Key] = slot.Value;
                foreach ((string path, bool kept) in new[] { (panePath, crazed), (layeredPath, layered) })
                    if (!kept && AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                        AssetDatabase.DeleteAsset(path);
            }
            return result;
        }

        /// <summary>The recipe the metadata names for a slot (its <c>materials</c> entry's <c>visual</c>).</summary>
        static string VisualOf(Dictionary<string, object> metaJson, string slot, string metaPath)
        {
            foreach (object item in Json.Arr(metaJson, "materials", metaPath))
            {
                var entry = Json.Obj(item, $"{metaPath} materials");
                if (Json.Str(entry, "slot", metaPath) == slot)
                    return Json.Str(entry, "visual", metaPath);
            }
            throw new FormatException($"{metaPath}: slot \"{slot}\" has no materials entry");
        }

        /// <summary>
        /// A layered recipe's <c>"layers"</c>, the list a damage mask composes over it (<see cref="EnsureLayeredMaterial"/>), and its
        /// optional <c>"base"</c>, the recipe whose surface they lie over instead of its own (p1-h01-adobe-house: the facade's
        /// whitewash, while its own material, what an unmasked slot draws, stays the bare adobe). When the mask's record names
        /// its <c>"channels"</c> (what R, G, B and A hold, <c>tools/blender/lib/meta.py</c>), they must be the recipe's
        /// <c>"channels"</c>, the names its layers read: a mask written for another surface is refused.
        /// </summary>
        static (List<object> layers, string baseRecipe) LayeredRecipe(string visual, Dictionary<string, object> maskRecord, string what)
        {
            string recipePath = $"{RecipesFolder}/{visual}.json";
            var recipe = Json.Obj(Json.Parse(ReadRepoFile(recipePath)), recipePath);
            if (!recipe.TryGetValue("layers", out object layers))
                throw new FormatException($"{what}: its recipe {visual} lists no \"layers\"; a damage mask needs a layered recipe");
            if (maskRecord.ContainsKey("channels"))
            {
                string[] mask = Names(maskRecord, what), read = recipe.ContainsKey("channels") ? Names(recipe, recipePath) : null;
                if (read == null)
                    throw new FormatException($"{what}: the mask names its channels ({string.Join(", ", mask)}) but its recipe {visual} names none to hold them to");
                if (!mask.SequenceEqual(read))
                    throw new FormatException($"{what}: the mask's channels (R, G, B, A) are {string.Join(", ", mask)}, but its recipe {visual}'s layers read {string.Join(", ", read)}");
            }
            return (Json.Arr(layers, $"{recipePath} \"layers\""), recipe.ContainsKey("base") ? Json.Str(recipe, "base", recipePath) : null);
        }

        static string[] Names(Dictionary<string, object> obj, string what) =>
            Json.Arr(obj, "channels", what).Select(c => c as string ?? throw new FormatException($"{what}: \"channels\" must be names")).ToArray();

        /// <summary>
        /// A crazed pane's material at <paramref name="path"/>, created or updated in place: its slot's plain glass (the
        /// transparent recipe's material) with the crack mask on <c>_CrackMask</c>, scaled and offset so the pane's UVs
        /// <paramref name="uvMin"/> and <paramref name="uvMax"/> fall on the mask's corners.
        /// </summary>
        static Material EnsureCrazedPaneMaterial(Material glass, Texture2D mask, Vector2 uvMin, Vector2 uvMax, string path, string what)
        {
            if (glass.shader.name != CrazedGlassShader)
                throw new FormatException($"{what}: its recipe {glass.name} is not transparent; a crack mask needs a \"blend\": \"transparent\" recipe");
            Vector2 size = uvMax - uvMin;
            if (size.x <= 0f || size.y <= 0f)
                throw new FormatException($"{what}: uv_max must lie above and right of uv_min, not {uvMin} to {uvMax}");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool changed = false;
            if (material == null)
            {
                material = new Material(glass.shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != glass.shader)
            {
                material.shader = glass.shader;
                changed = true;
            }
            changed |= SetTexture(material, "_BaseMap", glass.GetTexture("_BaseMap"));
            changed |= SetTextureScale(material, "_BaseMap", glass.GetTextureScale("_BaseMap"));
            changed |= SetTextureOffset(material, "_BaseMap", glass.GetTextureOffset("_BaseMap"));
            foreach (string colour in new[] { "_BaseColor", "_CrackColor" })
                changed |= SetColor(material, colour, glass.GetColor(colour));
            foreach (string value in new[] { "_Smoothness", "_IOR", "_CrackOpacity", "_CrackDepth" })
                changed |= SetFloat(material, value, glass.GetFloat(value));
            var scale = new Vector2(1f / size.x, 1f / size.y);
            changed |= SetTexture(material, "_CrackMask", mask);
            changed |= SetTextureScale(material, "_CrackMask", scale);
            changed |= SetTextureOffset(material, "_CrackMask", -Vector2.Scale(uvMin, scale));
            changed |= SetQueue(material, (int)RenderQueue.Transparent);
            if (changed)
                EditorUtility.SetDirty(material);
            return material;
        }

        // --- layered surfaces (p1-r01-vaz-sedan, design decision 5; p1-h01-adobe-house, design decision 2; tech-artist) ---

        internal const string LayeredSurfaceShader = "Fpv450/LayeredSurface";
        internal const int MaxLayers = 5; // the shader's: a facade's render, band, adobe, soot and dirt
        static readonly string[] LayerChannels = { "r", "g", "b", "a" };
        static readonly HashSet<string> LayerKeys = new HashSet<string> { "channel", "recipe", "threshold", "feather" };

        /// <summary>
        /// A layered surface's material at <paramref name="path"/>, created or updated in place, for
        /// <c>Shaders/LayeredSurface</c> (its header gives the rule): the slot's recipe material <paramref name="surface"/>
        /// as the base (its albedo and tiling, tint, normal map, and packed mask with its metallic and smoothness, as
        /// URP/Lit holds them); the damage mask scaled and offset so the slot's UVs <paramref name="uvMin"/> and
        /// <paramref name="uvMax"/> fall on its corners; and over the base, in order, the recipe's layers, 1 to
        /// <see cref="MaxLayers"/> of <c>{"channel": "r" | "g" | "b" | "a", "recipe": &lt;recipe&gt;, "threshold": 0–1,
        /// "feather": 0–1}</c>, each that recipe's own material's surface covering the share its channel of the mask
        /// sets: by default (threshold 0.5, feather 1) the channel's value. The base and every layer are opaque recipes
        /// drawn with URP/Lit, a tiled set or a palette (of one colour: its columns would stripe the metres), never clipped,
        /// transparent or ground. Internal for the EditMode tests, which give a layer list of their own.
        /// </summary>
        internal static Material EnsureLayeredMaterial(Material surface, List<object> layers, Dictionary<string, object> manifest,
            Texture2D mask, Vector2 uvMin, Vector2 uvMax, string path, string what)
        {
            CheckLayerSurface(surface, what);
            if (layers.Count == 0 || layers.Count > MaxLayers)
                throw new FormatException($"{what}: {layers.Count} layers; a layered surface takes 1 to {MaxLayers}");
            Vector2 size = uvMax - uvMin;
            if (size.x <= 0f || size.y <= 0f)
                throw new FormatException($"{what}: uv_max must lie above and right of uv_min, not {uvMin} to {uvMax}");
            var parsed = new List<(Material source, Vector4 channel, Vector4 coverage)>();
            for (int i = 0; i < layers.Count; i++)
            {
                string at = $"{what} layers[{i}]";
                var layer = Json.Obj(layers[i], at);
                foreach (string key in layer.Keys)
                    if (!LayerKeys.Contains(key))
                        throw new FormatException($"{at}: unknown key \"{key}\" (a layer takes {string.Join(", ", LayerKeys)})");
                int channel = Array.IndexOf(LayerChannels, Json.Str(layer, "channel", at));
                if (channel < 0)
                    throw new FormatException($"{at}: \"channel\" must be one of {string.Join(", ", LayerChannels)}");
                float threshold = layer.ContainsKey("threshold") ? Json.Num(layer, "threshold", at) : 0.5f;
                float feather = layer.ContainsKey("feather") ? Json.Num(layer, "feather", at) : 1f;
                if (threshold < 0f || threshold > 1f || feather <= 0f || feather > 1f)
                    throw new FormatException($"{at}: \"threshold\" must lie in 0–1 and \"feather\" in (0, 1], not {threshold} and {feather}");
                Material source = EnsureMaterial(Json.Str(layer, "recipe", at), manifest);
                CheckLayerSurface(source, at);
                var oneHot = Vector4.zero;
                oneHot[channel] = 1f;
                parsed.Add((source, oneHot, new Vector4(threshold, feather, 0f, 0f)));
            }

            Shader shader = Shader.Find(LayeredSurfaceShader) ?? throw new FileNotFoundException($"no shader named \"{LayeredSurfaceShader}\"");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool changed = false;
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                changed = true;
            }
            changed |= CopySurface(material, surface, "_BaseMap", "_BaseColor", "_BumpMap", "_MetallicGlossMap");
            Vector2 baseScales = MetallicSmoothness(surface);
            changed |= SetFloat(material, "_Metallic", baseScales.x);
            changed |= SetFloat(material, "_Smoothness", baseScales.y);
            var scale = new Vector2(1f / size.x, 1f / size.y);
            changed |= SetTexture(material, "_DamageMask", mask);
            changed |= SetTextureScale(material, "_DamageMask", scale);
            changed |= SetTextureOffset(material, "_DamageMask", -Vector2.Scale(uvMin, scale));

            for (int k = 1; k <= MaxLayers; k++)
            {
                string prefix = $"_Layer{k}";
                if (k > parsed.Count) // unused: covers nothing, binds nothing
                {
                    foreach (string texture in new[] { "Map", "BumpMap", "MaskMap" })
                        changed |= SetTexture(material, prefix + texture, null);
                    changed |= SetTextureScale(material, prefix + "Map", Vector2.one);
                    changed |= SetTextureOffset(material, prefix + "Map", Vector2.zero);
                    changed |= SetColor(material, prefix + "Color", Color.white);
                    changed |= SetVector(material, prefix + "Channel", Vector4.zero);
                    changed |= SetVector(material, prefix + "Coverage", new Vector4(1f, 1f, 0f, 0f));
                    changed |= SetVector(material, prefix + "Surface", new Vector4(0f, 0.5f, 0f, 0f));
                    continue;
                }
                (Material source, Vector4 channel, Vector4 coverage) = parsed[k - 1];
                Vector2 scales = MetallicSmoothness(source);
                changed |= CopySurface(material, source, prefix + "Map", prefix + "Color", prefix + "BumpMap", prefix + "MaskMap");
                changed |= SetVector(material, prefix + "Channel", channel);
                changed |= SetVector(material, prefix + "Coverage", coverage);
                changed |= SetVector(material, prefix + "Surface", new Vector4(scales.x, scales.y, 0f, 0f));
            }
            if (changed)
                EditorUtility.SetDirty(material);
            return material;
        }

        static void CheckLayerSurface(Material m, string what)
        {
            if (m.shader.name != LitShader || m.IsKeywordEnabled("_ALPHATEST_ON") || m.IsKeywordEnabled("_DETAIL_MULX2"))
                throw new FormatException($"{what}: {m.name} is not an opaque tiled set or palette drawn with {LitShader}, which a layered surface takes");
        }

        /// <summary>A URP/Lit material's surface onto a layered one's properties of the given names.</summary>
        static bool CopySurface(Material to, Material from, string albedo, string tint, string normal, string mask)
        {
            bool changed = SetTexture(to, albedo, from.GetTexture("_BaseMap"));
            changed |= SetTextureScale(to, albedo, from.GetTextureScale("_BaseMap"));
            changed |= SetTextureOffset(to, albedo, from.GetTextureOffset("_BaseMap"));
            changed |= SetColor(to, tint, from.GetColor("_BaseColor"));
            changed |= SetTexture(to, normal, from.GetTexture("_BumpMap"));
            changed |= SetTexture(to, mask, from.GetTexture("_MetallicGlossMap"));
            return changed;
        }

        /// <summary>
        /// What scales a URP/Lit material's packed mask into its metallic and smoothness: with the mask, 1 and its
        /// smoothness (1, <see cref="EnsureMaterial(string, Dictionary{string, object}, string)"/> sets); without, its
        /// constants, which the shader's white default mask passes through.
        /// </summary>
        static Vector2 MetallicSmoothness(Material m) =>
            new Vector2(m.IsKeywordEnabled("_METALLICSPECGLOSSMAP") ? 1f : m.GetFloat("_Metallic"), m.GetFloat("_Smoothness"));

        static bool SetVector(Material m, string name, Vector4 value)
        {
            if (m.GetVector(name) == value)
                return false;
            m.SetVector(name, value);
            return true;
        }

        /// <summary>A crack mask, imported as linear data and clamped, so the pane outside the mask has no cracks.</summary>
        static Texture2D CrackMask(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new FileNotFoundException($"{path} is not a texture in the asset database: run the Blender build first");
            if (importer.textureType != TextureImporterType.Default || importer.sRGBTexture || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new FileNotFoundException($"{path} did not import as a texture");
        }

        // --- the prefab -------------------------------------------------------------------------------------------

        /// <summary>
        /// Creates or updates the prefab in place (so its file ids stay stable): the metadata component, one child
        /// per LOD with the FBX mesh and the slot materials plus one per micro mesh of that LOD (rendered with it,
        /// never a collider), the LODGroup, one child per collision primitive, and nothing else. Every slot of a LOD
        /// whose metadata entry is <c>thin_wire</c> draws with the thin-wire material made from its recipe's
        /// (<see cref="EnsureThinWireMaterial"/>), and every vertex of it must carry its radius in uv2, the shader's
        /// input: a wire's tube, and a pole's far LODs (p1-l01-l03-pole-wires, "Night silhouette kept to 1 km"). Every
        /// other slot, and every micro mesh, draws with the recipe's own, whatever its uv2 holds (Unity's lightmap
        /// channel; review S2). A wire's tube ("Wires are audited against their polyline") is the faces of the slot
        /// its <c>wires</c> record names: it draws only in a <c>thin_wire</c> LOD, a renderer drawing only wires casts
        /// no shadow (any other casts one, the thin-wire shader's at the member's true radius), and no collider
        /// stands for a wire: conductors are map objects the sim reads from the metadata (D-007). A slot with a crack mask
        /// or a damage mask under the metadata's <c>textures</c> draws with its crazed pane's or its layered surface's
        /// own material (<see cref="WithMaskedSlots"/>).
        /// Internal for the EditMode tests, which build a prefab from a throwaway metadata.
        /// </summary>
        internal static void EnsurePrefab(string prefabPath, string variant, Dictionary<string, object> metaJson, TextAsset source,
            string fbxPath, Dictionary<string, Material> materialBySlot)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject(variant);
            try
            {
                root.name = variant;
                AssetPhysicsMeta meta = Ensure<AssetPhysicsMeta>(root);
                Fill(meta, metaJson, source, prefabPath);
                materialBySlot = WithMaskedSlots(prefabPath, variant, metaJson, AssetDatabase.GetAssetPath(source), materialBySlot);
                var wireSlots = new HashSet<string>(meta.wires.Select(w => w.slot));
                var keep = new HashSet<string>();
                var thin = new List<string>();
                Renderer[][] renderers = EnsureLods(root, meta, fbxPath, materialBySlot, wireSlots, keep, thin);
                EnsureLodGroup(root, meta, renderers);
                EnsureColliders(root, meta, keep, prefabPath);
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = root.transform.GetChild(i).gameObject;
                    if (!keep.Contains(child.name))
                        Object.DestroyImmediate(child);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
                if (!saved)
                    throw new IOException($"could not save {prefabPath}");
                string micro = meta.micro.Length == 0 ? "" : $"{string.Join(", ", meta.micro.Select(m => $"{m.name} ({m.count} {m.kind}, LOD{m.lod})"))} micro, ";
                string ports = meta.ports.Length == 0 ? "" : $"{meta.ports.Length} ports ({string.Join(", ", meta.ports.Select(p => p.id))}), ";
                string wires = meta.wires.Length == 0 ? "" : $"{meta.wires.Length} wires without colliders ({string.Join(", ", meta.wires.GroupBy(w => (w.slot, w.diameterM)).Select(g => $"{g.Count()} x {g.Key.diameterM * 1000f:0.#} mm in {g.Key.slot} -> {ThinWireShader}"))}), ";
                string thinWire = thin.Count == 0 ? "" : $"thin wire {string.Join(", ", thin)}, ";
                string holes = meta.holes.Length == 0 ? "" : $"{meta.holes.Length} holes ({string.Join(", ", meta.holes.Select(h => $"{h.id} {h.centreLine.Length} points, band {h.bandWidthM:0.###} m"))}), ";
                Debug.Log($"[CatalogImporter] {variant}: {prefabPath}, {renderers.Length} LODs, {micro}{meta.collision.Length} colliders, {ports}{wires}{holes}{thinWire}" +
                          $"materials {string.Join(", ", materialBySlot.Select(kv => $"{kv.Key} -> {kv.Value.name}"))}, " +
                          $"LOD heights {string.Join(" / ", root.GetComponent<LODGroup>().GetLODs().Select(l => l.screenRelativeTransitionHeight.ToString("0.0000")))} " +
                          $"(size {root.GetComponent<LODGroup>().size} m, fov {VerticalFovDeg} deg)");
            }
            finally
            {
                if (exists)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    Object.DestroyImmediate(root);
            }
        }

        /// <summary>Per LOD, its mesh's renderer followed by those of the micro meshes listed for it.</summary>
        static Renderer[][] EnsureLods(GameObject root, AssetPhysicsMeta meta, string fbxPath, Dictionary<string, Material> materialBySlot,
            HashSet<string> wireSlots, HashSet<string> keep, List<string> thin)
        {
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath)
                ?? throw new FileNotFoundException($"{fbxPath} did not import as a model");
            MeshFilter[] filters = imported.GetComponentsInChildren<MeshFilter>(true);
            var renderers = new Renderer[meta.lods.Length][];
            for (int i = 0; i < meta.lods.Length; i++)
            {
                var lod = new List<Renderer> { MeshChild(root, meta.lods[i].mesh, meta.lods[i].thinWire, filters, fbxPath, materialBySlot, wireSlots, keep, thin) };
                foreach (AssetPhysicsMeta.MicroMesh micro in meta.micro)
                    if (micro.lod == i)
                        lod.Add(MeshChild(root, micro.name, false, filters, fbxPath, materialBySlot, wireSlots, keep, thin));
                renderers[i] = lod.ToArray();
            }
            return renderers;
        }

        static Renderer MeshChild(GameObject root, string meshName, bool thinWire, MeshFilter[] filters, string fbxPath,
            Dictionary<string, Material> materialBySlot, HashSet<string> wireSlots, HashSet<string> keep, List<string> thin)
        {
            MeshFilter filter = filters.FirstOrDefault(f => f.sharedMesh != null && f.sharedMesh.name == meshName)
                ?? throw new FormatException($"{fbxPath} has no mesh named {meshName}");
            string[] slots = filter.GetComponent<MeshRenderer>().sharedMaterials.Select(slot => slot.name).ToArray();
            var uv2 = new List<Vector2>();
            filter.sharedMesh.GetUVs(1, uv2);
            var slotMaterials = new Material[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                if (!materialBySlot.TryGetValue(slots[i], out Material material))
                    throw new FormatException($"{meshName}: the FBX slot \"{slots[i]}\" has no material in the metadata");
                if (wireSlots.Contains(slots[i]) && !thinWire)
                    throw new FormatException($"{meshName} slot {slots[i]}: a wire's tube draws only in a LOD whose metadata entry is thin_wire");
                if (thinWire && !CarriesRadius(filter.sharedMesh, uv2, i))
                    throw new FormatException($"{meshName} slot {slots[i]}: a thin_wire LOD needs its radius (> 0 m) in uv2 on every vertex for the " +
                                              $"thin-wire shader ({uv2.Count} uv2 values over {filter.sharedMesh.vertexCount} vertices)");
                slotMaterials[i] = thinWire ? EnsureThinWireMaterial(material) : material;
                if (thinWire)
                    thin.Add($"{meshName} {slots[i]}");
            }

            GameObject child = Child(root, meshName, keep);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            Ensure<MeshFilter>(child).sharedMesh = filter.sharedMesh;
            MeshRenderer renderer = Ensure<MeshRenderer>(child);
            renderer.sharedMaterials = slotMaterials;
            renderer.shadowCastingMode = slots.Length > 0 && slots.All(wireSlots.Contains) ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return renderer;
        }

        /// <summary>
        /// Whether every vertex of a submesh carries a radius (> 0 m) in uv2, the thin-wire shader's input (written by
        /// lib/export from the generator's second UV): without it the shader draws a tube of radius 0, nothing. A
        /// check on a <c>thin_wire</c> LOD only, never what picks the material.
        /// </summary>
        static bool CarriesRadius(Mesh mesh, List<Vector2> uv2, int submesh)
        {
            int[] indices = mesh.GetIndices(submesh);
            return indices.Length > 0 && uv2.Count > 0 && indices.All(i => uv2[i].x > 0f);
        }

        /// <summary>
        /// Unity draws LOD i while the object's screen-relative height stays above that LOD's transition height
        /// (scaled by the quality level's lodBias), and culls it below the last one. The height of an object of
        /// size s at distance d is s / (2 d tan(fov / 2)), so each metadata distance becomes that height, with s
        /// the longest side of the bounds, which is also what the group's size is set to. Unity holds transition
        /// heights to (0, 1], so an asset taller than the screen at its first switch distance (a 12 m ground patch
        /// switching at 8 m: 1.3) would be clamped and switch late; there s is reduced until that first height is
        /// 1. Only the ratio s / d reaches Unity, so every switch distance is kept either way.
        /// </summary>
        static void EnsureLodGroup(GameObject root, AssetPhysicsMeta meta, Renderer[][] renderers)
        {
            float firstFar = renderers.Length > 1 ? meta.lods[1].switchDistanceM : meta.cullDistanceM;
            float size = Mathf.Min(Mathf.Max(meta.bounds.size.x, meta.bounds.size.y, meta.bounds.size.z),
                2f * firstFar * Mathf.Tan(VerticalFovDeg * 0.5f * Mathf.Deg2Rad));
            var lods = new LOD[renderers.Length];
            for (int i = 0; i < lods.Length; i++)
            {
                float farDistance = i + 1 < lods.Length ? meta.lods[i + 1].switchDistanceM : meta.cullDistanceM;
                lods[i] = new LOD(RelativeHeight(size, farDistance), renderers[i]);
            }
            LODGroup group = Ensure<LODGroup>(root);
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(lods); // recomputes the size and reference point from the renderers, so they are set after it
            group.localReferencePoint = meta.bounds.center;
            group.size = size;
        }

        static float RelativeHeight(float size, float distance) =>
            size / (2f * distance * Mathf.Tan(VerticalFovDeg * 0.5f * Mathf.Deg2Rad));

        /// <summary>
        /// One child per primitive, placed and rotated as the metadata says, with the matching Unity collider. A
        /// cylinder, which Unity has no primitive for, gets a convex MeshCollider on a generated cylinder mesh
        /// (<see cref="CylinderMesh"/>), for noclip only: the sim reads the metadata. No physic material: the
        /// material id lives on the metadata component.
        /// </summary>
        static void EnsureColliders(GameObject root, AssetPhysicsMeta meta, HashSet<string> keep, string prefabPath)
        {
            var cylinderMeshes = new Dictionary<string, Mesh>();
            for (int i = 0; i < meta.collision.Length; i++)
            {
                AssetPhysicsMeta.CollisionShape shape = meta.collision[i];
                Type type = shape.kind switch
                {
                    AssetPhysicsMeta.ShapeKind.Box => typeof(BoxCollider),
                    AssetPhysicsMeta.ShapeKind.Sphere => typeof(SphereCollider),
                    AssetPhysicsMeta.ShapeKind.Capsule => typeof(CapsuleCollider),
                    AssetPhysicsMeta.ShapeKind.Cylinder => typeof(MeshCollider),
                    _ => throw new NotSupportedException($"{root.name} collision[{i}]: unknown shape kind {shape.kind}"),
                };
                GameObject host = Child(root, $"Collision{i}", keep);
                host.transform.localPosition = shape.center;
                host.transform.localRotation = shape.rotation;
                host.transform.localScale = Vector3.one;
                foreach (Collider stale in host.GetComponents<Collider>())
                    if (stale.GetType() != type)
                        Object.DestroyImmediate(stale);
                var collider = (Collider)host.GetComponent(type);
                if (collider == null)
                    collider = (Collider)host.AddComponent(type);
                switch (collider)
                {
                    case BoxCollider box:
                        box.center = Vector3.zero;
                        box.size = shape.size;
                        break;
                    case SphereCollider sphere:
                        sphere.center = Vector3.zero;
                        sphere.radius = shape.radius;
                        break;
                    case CapsuleCollider capsule:
                        capsule.center = Vector3.zero;
                        capsule.radius = shape.radius;
                        capsule.height = shape.height;
                        capsule.direction = 1; // Y; the child's rotation carries the axis
                        break;
                    case MeshCollider cylinder:
                        cylinder.sharedMesh = CylinderMesh(prefabPath, root.name, shape.radius, shape.height, cylinderMeshes);
                        cylinder.convex = true;
                        break;
                }
            }

            string folder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            foreach (string file in Directory.GetFiles(FullPath(folder), $"{root.name}_cylinder_*.asset"))
            {
                string stale = $"{folder}/{Path.GetFileName(file)}";
                if (!cylinderMeshes.ContainsKey(stale))
                    AssetDatabase.DeleteAsset(stale);
            }
        }

        /// <summary>
        /// The mesh of a cylinder collider: a closed polygonal cylinder of the metadata's radius and height along
        /// +Y, centred on the origin like the shape, wound clockwise seen from outside. It is an asset beside the
        /// prefab, <c>&lt;variant&gt;_cylinder_r&lt;radius&gt;_h&lt;height&gt;.asset</c>, shared by every cylinder of the
        /// variant with that radius and height, created once and rewritten only when its vertices change, so a
        /// rebuild with unchanged inputs changes no file.
        /// </summary>
        static Mesh CylinderMesh(string prefabPath, string variant, float radius, float height, Dictionary<string, Mesh> cache)
        {
            string name = $"{variant}_cylinder_r{radius.ToString("R", CultureInfo.InvariantCulture)}_h{height.ToString("R", CultureInfo.InvariantCulture)}";
            string path = $"{Path.GetDirectoryName(prefabPath).Replace('\\', '/')}/{name}.asset";
            if (cache.TryGetValue(path, out Mesh cached))
                return cached;

            // r·(1 − cos(π/N)) ≤ tolerance  ⇔  N ≥ π / acos(1 − tolerance / r)
            int segments = Mathf.CeilToInt(Mathf.PI / Mathf.Acos(Mathf.Max(-1f, 1f - CylinderToleranceM / radius)));
            segments = Mathf.Clamp((segments + 3) / 4 * 4, CylinderMinSegments, CylinderMaxSegments);
            float deviation = radius * (1f - Mathf.Cos(Mathf.PI / segments));
            if (deviation > CylinderToleranceM)
                Debug.LogWarning($"[CatalogImporter] {name}: {segments} segments (the convex hull's cap) sit {deviation * 1000f:0.0} mm " +
                                 $"inside the true surface, beyond the {CylinderToleranceM * 1000f:0} mm tolerance");

            var vertices = new Vector3[2 * segments]; // the bottom ring, then the top ring
            for (int i = 0; i < segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                float x = radius * Mathf.Cos(angle), z = radius * Mathf.Sin(angle);
                vertices[i] = new Vector3(x, -height / 2f, z);
                vertices[segments + i] = new Vector3(x, height / 2f, z);
            }
            var triangles = new int[(4 * segments - 4) * 3];
            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int b0 = i, b1 = (i + 1) % segments, t0 = segments + b0, t1 = segments + b1;
                triangles[t++] = b0; triangles[t++] = t0; triangles[t++] = t1;
                triangles[t++] = b0; triangles[t++] = t1; triangles[t++] = b1;
            }
            for (int i = 1; i < segments - 1; i++) // the caps, fans from each ring's first vertex
            {
                triangles[t++] = 0; triangles[t++] = i; triangles[t++] = i + 1;
                triangles[t++] = segments; triangles[t++] = segments + i + 1; triangles[t++] = segments + i;
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }
            else if (!SameVertices(mesh.vertices, vertices) || !mesh.triangles.SequenceEqual(triangles))
            {
                mesh.Clear();
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
            }
            cache[path] = mesh;
            return mesh;
        }

        static bool SameVertices(Vector3[] a, Vector3[] b) =>
            a.Length == b.Length && a.Zip(b, (p, q) => (p - q).sqrMagnitude <= 1e-12f).All(same => same);

        static GameObject Child(GameObject root, string name, HashSet<string> keep)
        {
            keep.Add(name);
            Transform existing = root.transform.Find(name);
            if (existing != null)
                return existing.gameObject;
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child;
        }

        // Unity's missing-component null is not a C# null, so no ?? here.
        static T Ensure<T>(GameObject on) where T : Component
        {
            T component = on.GetComponent<T>();
            return component != null ? component : on.AddComponent<T>();
        }

        // --- the metadata component -------------------------------------------------------------------------------

        static void Fill(AssetPhysicsMeta meta, Dictionary<string, object> json, TextAsset source, string what)
        {
            meta.schema = (int)Json.Num(json, "schema", what);
            if (meta.schema != 1)
                throw new NotSupportedException($"{what}: schema {meta.schema}; this importer reads schema 1");
            meta.id = Json.Str(json, "id", what);
            meta.variant = Json.Str(json, "variant", what);
            meta.generator = Json.Str(json, "generator", what);
            meta.paramsSha256 = Json.Str(json, "params_sha256", what);
            meta.units = Json.Str(json, "units", what);
            meta.frame = Json.Str(json, "frame", what);
            if (meta.units != "m" || meta.frame != "unity-lh-yup")
                throw new NotSupportedException($"{what}: units/frame {meta.units}/{meta.frame}; expected m/unity-lh-yup (D-019)");

            var bounds = Json.Obj(json, "bounds", what);
            Vector3 min = Json.Vec3(bounds, "min", what), max = Json.Vec3(bounds, "max", what);
            meta.bounds = new Bounds((min + max) * 0.5f, max - min);

            meta.lods = Items(json, "lods", what, (lod, at) => new AssetPhysicsMeta.LodEntry
            {
                mesh = Json.Str(lod, "mesh", at),
                tris = (int)Json.Num(lod, "tris", at),
                switchDistanceM = Json.Num(lod, "switch_distance_m", at),
                thinWire = Json.Bool(lod, "thin_wire", at),
            });
            if (meta.lods.Length == 0)
                throw new FormatException($"{what}: no LODs");
            meta.micro = Items(json, "micro", what, (micro, at) =>
            {
                var entry = new AssetPhysicsMeta.MicroMesh
                {
                    lod = (int)Json.Num(micro, "lod", at),
                    name = Json.Str(micro, "name", at),
                    kind = Json.Str(micro, "kind", at),
                    count = (int)Json.Num(micro, "count", at),
                    tris = (int)Json.Num(micro, "tris", at),
                };
                if (entry.lod < 0 || entry.lod >= meta.lods.Length)
                    throw new FormatException($"{at}: micro mesh {entry.name} names LOD {entry.lod}, which does not exist");
                return entry;
            });
            meta.cullDistanceM = Json.Num(json, "cull_distance_m", what);
            meta.materials = Items(json, "materials", what, (slot, at) => new AssetPhysicsMeta.MaterialSlot
            {
                slot = Json.Str(slot, "slot", at),
                visual = Json.Str(slot, "visual", at),
                physics = Json.Str(slot, "physics", at),
            });
            meta.collision = Items(json, "collision", what, (shape, at) =>
            {
                var collision = new AssetPhysicsMeta.CollisionShape
                {
                    material = Json.Str(shape, "material", at),
                    landable = Json.Bool(shape, "landable", at),
                };
                ReadShape(collision, shape, at);
                return collision;
            });
            meta.gaps = Items(json, "gaps", what, (gap, at) => new AssetPhysicsMeta.Gap
            {
                id = Json.Str(gap, "id", at),
                polygon = Json.Arr(gap, "polygon", at).Select(p => Json.Vec3(p, $"{at} polygon")).ToArray(),
                normal = Json.Vec3(gap, "normal", at),
                narrowestM = Json.Num(gap, "narrowest_m", at),
            });
            meta.windVolumes = Items(json, "wind_volumes", what, (shape, at) =>
            {
                var volume = new AssetPhysicsMeta.WindVolume { porosity = Json.Num(shape, "porosity", at) };
                ReadShape(volume, shape, at);
                return volume;
            });
            meta.ports = Items(json, "ports", what, (port, at) => new AssetPhysicsMeta.Port
            {
                id = Json.Str(port, "id", at),
                position = Json.Vec3(port, "position", at),
                direction = Json.Vec3(port, "direction", at),
                maxDiameterM = Json.Num(port, "max_diameter_m", at),
            });
            meta.wires = Items(json, "wires", what, (wire, at) => new AssetPhysicsMeta.Wire
            {
                slot = Json.Str(wire, "slot", at),
                diameterM = Json.Num(wire, "diameter_m", at),
                massKgPerM = Json.Num(wire, "mass_kg_per_m", at),
                sagM = Json.Num(wire, "sag_m", at),
                axialStiffnessN = Json.Num(wire, "axial_stiffness_n", at),
                breakingForceN = Json.Num(wire, "breaking_force_n", at),
                material = Json.Str(wire, "material", at),
                damage = Json.Str(wire, "damage", at),
                points = Json.Arr(wire, "points", at).Select(p => Json.Vec3(p, $"{at} points")).ToArray(),
            });
            meta.holes = Items(json, "holes", what, (hole, at) => new AssetPhysicsMeta.Hole
            {
                id = Json.Str(hole, "id", at),
                centreLine = Json.Arr(hole, "centre_line", at).Select(p => Json.Vec3(p, $"{at} centre_line")).ToArray(),
                topWidthM = Json.Num(hole, "top_width_m", at),
                depthM = Json.Num(hole, "depth_m", at),
                bandWidthM = Json.Num(hole, "band_width_m", at),
            });
            meta.snag = Items(json, "snag", what, (feature, at) => new AssetPhysicsMeta.SnagFeature
            {
                kind = Json.Str(feature, "kind", at),
                points = Json.Arr(feature, "points", at).Select(p => Json.Vec3(p, $"{at} points")).ToArray(),
                radiusM = Json.Num(feature, "radius_m", at),
            });
            meta.sourceJson = source != null ? source : throw new FileNotFoundException($"{what}: the metadata did not import as a TextAsset");
        }

        static T[] Items<T>(Dictionary<string, object> json, string key, string what, Func<Dictionary<string, object>, string, T> read) =>
            Json.Arr(json, key, what).Select((item, i) => read(Json.Obj(item, $"{what} {key}[{i}]"), $"{what} {key}[{i}]")).ToArray();

        static void ReadShape(AssetPhysicsMeta.Shape target, Dictionary<string, object> json, string what)
        {
            string kind = Json.Str(json, "shape", what);
            target.kind = kind switch
            {
                "box" => AssetPhysicsMeta.ShapeKind.Box,
                "sphere" => AssetPhysicsMeta.ShapeKind.Sphere,
                "capsule" => AssetPhysicsMeta.ShapeKind.Capsule,
                "cylinder" => AssetPhysicsMeta.ShapeKind.Cylinder,
                _ => throw new FormatException($"{what}: unknown shape \"{kind}\""),
            };
            bool box = target.kind == AssetPhysicsMeta.ShapeKind.Box;
            bool sphere = target.kind == AssetPhysicsMeta.ShapeKind.Sphere;
            target.center = Json.Vec3(json, "center", what);
            target.size = box ? Json.Vec3(json, "size", what) : Vector3.zero;
            target.radius = box ? 0f : Json.Num(json, "radius", what);
            target.height = box || sphere ? 0f : Json.Num(json, "height", what);
            target.rotation = sphere ? Quaternion.identity : Json.Quat(json, "rotation", what);
        }
    }
}
