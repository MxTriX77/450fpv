using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Feed
{
    /// <summary>
    /// Writes <see cref="WetShaders"/>'s asset and its variant collection before every player build (w21-rain-on-camera
    /// 1.3), so the build carries the wet copies with every material keyword set they will draw:
    /// <list type="bullet">
    /// <item>the wet Lit's: each distinct keyword set of the project's URP/Lit materials (the twins keep their originals');</item>
    /// <item>the wet terrain's three shaders': every set of the keywords a terrain turns on from its layers and template
    /// (_NORMALMAP, _MASKMAP, _TERRAIN_INSTANCED_PERPIXEL_NORMAL), eight sets.</item>
    /// </list>
    /// <c>-batchmode -quit -executeMethod Fpv450.Feed.WetVariants.Write</c> writes them alone; WetVariantsTests holds the
    /// committed collection to the project's materials.
    /// </summary>
    public sealed class WetVariants : IPreprocessBuildWithReport
    {
        public const string Folder = "Assets/_Project/Settings/Wet/Resources";
        public const string AssetPath = Folder + "/" + WetShaders.ResourceName + ".asset";
        public const string VariantsPath = Folder + "/WetVariants.shadervariants";

        static readonly string[] TerrainKeywords = { "_NORMALMAP", "_MASKMAP", "_TERRAIN_INSTANCED_PERPIXEL_NORMAL" };

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Write();

        public static void Write()
        {
            Shader lit = Find(WetLook.WetLitShader), terrain = Find(WetLook.WetTerrainShader);
            Shader add = Find("Hidden/Fpv450/Wet/Terrain Lit (Add Pass)"), basePass = Find("Hidden/Fpv450/Wet/Terrain Lit (Base Pass)");
            Shader extinction = Find(WetLook.ExtinctionShader);

            var collection = new ShaderVariantCollection();
            int count = 0;
            foreach (string[] keywords in LitKeywordSets())
                count += Add(collection, lit, keywords);
            for (int set = 0; set < 1 << TerrainKeywords.Length; set++)
            {
                string[] keywords = TerrainKeywords.Where((_, i) => (set & (1 << i)) != 0).ToArray();
                count += Add(collection, terrain, keywords) + Add(collection, add, keywords) + Add(collection, basePass, keywords);
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings/Wet"))
                    AssetDatabase.CreateFolder("Assets/_Project/Settings", "Wet");
                AssetDatabase.CreateFolder("Assets/_Project/Settings/Wet", "Resources");
            }
            var existing = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(VariantsPath);
            if (existing == null)
                AssetDatabase.CreateAsset(collection, VariantsPath);
            else
            {
                EditorUtility.CopySerialized(collection, existing);
                Object.DestroyImmediate(collection);
                collection = existing;
            }

            var shaders = AssetDatabase.LoadAssetAtPath<WetShaders>(AssetPath);
            if (shaders == null)
            {
                shaders = ScriptableObject.CreateInstance<WetShaders>();
                AssetDatabase.CreateAsset(shaders, AssetPath);
            }
            shaders.lit = lit;
            shaders.terrain = terrain;
            shaders.terrainAdd = add;
            shaders.terrainBase = basePass;
            shaders.extinction = extinction;
            shaders.variants = collection;
            EditorUtility.SetDirty(collection);
            EditorUtility.SetDirty(shaders);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WetVariants] {AssetPath}: the wet shaders and {count} variants ({collection.variantCount} in {VariantsPath})");
        }

        /// <summary>The distinct keyword sets of the project's URP/Lit materials, each sorted.</summary>
        public static List<string[]> LitKeywordSets()
        {
            var sets = new Dictionary<string, string[]>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null || m.shader == null || m.shader.name != WetLook.LitShader)
                    continue;
                string[] keywords = m.shaderKeywords.OrderBy(k => k, System.StringComparer.Ordinal).ToArray();
                sets[string.Join(" ", keywords)] = keywords;
            }
            return sets.OrderBy(p => p.Key, System.StringComparer.Ordinal).Select(p => p.Value).ToList();
        }

        static int Add(ShaderVariantCollection collection, Shader shader, string[] keywords)
        {
            var variant = new ShaderVariantCollection.ShaderVariant { shader = shader, passType = PassType.ScriptableRenderPipeline, keywords = keywords };
            return collection.Add(variant) ? 1 : 0;
        }

        static Shader Find(string name) =>
            Shader.Find(name) ?? throw new BuildFailedException($"[WetVariants] the shader {name} is missing");
    }
}
