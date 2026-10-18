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
        /// tiling is size/tile_size_m: the detail repeats every tile_size_m metres of the asset.
        /// </para>
        /// <para>
        /// A recipe of kind <c>palette</c> (the straw instance kit's tints) has no texture set: its base map is the
        /// flat <c>&lt;visual&gt;_albedo.png</c> that <c>derive.py</c> writes from its colours, one block per colour,
        /// at tiling 1 (a generator UVs each face into its column), with the recipe's constant smoothness and no
        /// normal or mask.
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
                float size = Json.Num(recipe, "size_m", recipePath);
                var detail = Json.Obj(recipe, "detail", recipePath);
                float detailTile = Json.Num(detail, "tile_size_m", recipePath);
                if (size <= 0f || detailTile <= 0f)
                    throw new FormatException($"{recipePath}: size_m and detail.tile_size_m must be positive metres");
                albedo = Derived(visual, "albedo");
                normal = Derived(visual, "normal");
                mask = Derived(visual, "mask");
                detailAlbedo = Derived(visual, "detail_albedo");
                Dictionary<string, Texture2D> detailSet = TextureSet(Json.Str(detail, "texture_set", recipePath), manifest, recipePath);
                if (!detailSet.TryGetValue("normal", out detailNormal))
                    throw new FormatException($"{recipePath}: the detail texture set has no normal map");
                baseScale = new Vector2(1f / size, 1f / size);
                baseOffset = new Vector2(0.5f, 0.5f);
                detailScale = new Vector2(size / detailTile, size / detailTile);
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
            if (m.GetTexture(name) == value)
                return false;
            m.SetTexture(name, value);
            return true;
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

        // --- the prefab -------------------------------------------------------------------------------------------

        /// <summary>
        /// Creates or updates the prefab in place (so its file ids stay stable): the metadata component, one child
        /// per LOD with the FBX mesh and the slot materials plus one per micro mesh of that LOD (rendered with it,
        /// never a collider), the LODGroup, one child per collision primitive, and nothing else. A wire's tube
        /// (p1-l01-l03-pole-wires, "Wires are audited against their polyline") is drawn by the LOD meshes in the slot
        /// its <c>wires</c> record names, with that slot's recipe material (a recipe naming the thin-wire shader gets
        /// it), and no collider stands for it: conductors are map objects the sim reads from the metadata (D-007).
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
                var keep = new HashSet<string>();
                Renderer[][] renderers = EnsureLods(root, meta, fbxPath, materialBySlot, keep);
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
                string wires = meta.wires.Length == 0 ? "" : $"{meta.wires.Length} wires without colliders ({string.Join(", ", meta.wires.GroupBy(w => (w.slot, w.diameterM)).Select(g => $"{g.Count()} x {g.Key.diameterM * 1000f:0.#} mm in {g.Key.slot} -> {materialBySlot[g.Key.slot].shader.name}"))}), ";
                Debug.Log($"[CatalogImporter] {variant}: {prefabPath}, {renderers.Length} LODs, {micro}{meta.collision.Length} colliders, {ports}{wires}" +
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
        static Renderer[][] EnsureLods(GameObject root, AssetPhysicsMeta meta, string fbxPath, Dictionary<string, Material> materialBySlot, HashSet<string> keep)
        {
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath)
                ?? throw new FileNotFoundException($"{fbxPath} did not import as a model");
            MeshFilter[] filters = imported.GetComponentsInChildren<MeshFilter>(true);
            var renderers = new Renderer[meta.lods.Length][];
            for (int i = 0; i < meta.lods.Length; i++)
            {
                var lod = new List<Renderer> { MeshChild(root, meta.lods[i].mesh, filters, fbxPath, materialBySlot, keep) };
                foreach (AssetPhysicsMeta.MicroMesh micro in meta.micro)
                    if (micro.lod == i)
                        lod.Add(MeshChild(root, micro.name, filters, fbxPath, materialBySlot, keep));
                renderers[i] = lod.ToArray();
            }
            return renderers;
        }

        static Renderer MeshChild(GameObject root, string meshName, MeshFilter[] filters, string fbxPath, Dictionary<string, Material> materialBySlot, HashSet<string> keep)
        {
            MeshFilter filter = filters.FirstOrDefault(f => f.sharedMesh != null && f.sharedMesh.name == meshName)
                ?? throw new FormatException($"{fbxPath} has no mesh named {meshName}");
            Material[] slotMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials
                .Select(slot => materialBySlot.TryGetValue(slot.name, out Material material)
                    ? material
                    : throw new FormatException($"{meshName}: the FBX slot \"{slot.name}\" has no material in the metadata"))
                .ToArray();

            GameObject child = Child(root, meshName, keep);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            Ensure<MeshFilter>(child).sharedMesh = filter.sharedMesh;
            MeshRenderer renderer = Ensure<MeshRenderer>(child);
            renderer.sharedMaterials = slotMaterials;
            return renderer;
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
            });
            meta.wires = Items(json, "wires", what, (wire, at) => new AssetPhysicsMeta.Wire
            {
                slot = Json.Str(wire, "slot", at),
                diameterM = Json.Num(wire, "diameter_m", at),
                massKgPerM = Json.Num(wire, "mass_kg_per_m", at),
                sagM = Json.Num(wire, "sag_m", at),
                material = Json.Str(wire, "material", at),
                damage = Json.Str(wire, "damage", at),
                points = Json.Arr(wire, "points", at).Select(p => Json.Vec3(p, $"{at} points")).ToArray(),
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
