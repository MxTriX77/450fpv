using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Fpv450.Feed
{
    /// <summary>The surfaces' moisture as the sim reads it per rain (SurfaceTable.MoistureOf, D-021): Sunny Dry, Rain
    /// Wet, Severe rain Saturated.</summary>
    public enum WetState { Dry = 0, Wet = 1, Saturated = 2 }

    /// <summary>
    /// The wet look (w21-rain-on-camera 1.3, VIS-03, D-126): one input, the sim's moisture for the flight's rain, which
    /// every lit shader reads through the global keyword <see cref="Keyword"/> and the globals of
    /// <c>Shaders/WetLaw.hlsl</c>, with the numbers of <see cref="WetOptics"/>. Wetted materials darken by Lekner and
    /// Dorf's law from their own albedo and show the sky through water's top; Severe rain adds standing water on the
    /// hard, flat faces (renderers the caller names, which carry <see cref="HardLayer"/> while it rains); the rain's
    /// extinction from its drop spectrum dims the distance (<c>Shaders/RainExtinction.shader</c>).
    /// <list type="bullet">
    /// <item>URP/Lit and URP's Terrain/Lit, which the project can't edit, are drawn through wet copies of themselves
    /// (<c>Shaders/Wet/</c>): each renderer's URP/Lit material is swapped for a twin of it on the copy, each terrain's
    /// template likewise, and both are put back by <see cref="Restore"/>.</item>
    /// <item>Sunny touches nothing: <see cref="Apply"/> with <see cref="WetState.Dry"/> only undoes an earlier rain, so
    /// a dry flight draws exactly what it drew before the wet look existed.</item>
    /// </list>
    /// </summary>
    public static class WetLook
    {
        public const string Keyword = "FPV450_WET";

        /// <summary>The rendering-layer bit of a hard surface while it rains (WetLaw.hlsl's FPV450_WET_HARD_LAYER).</summary>
        public const int HardLayer = 7;

        public const string LitShader = "Universal Render Pipeline/Lit", TerrainShader = "Universal Render Pipeline/Terrain/Lit";
        public const string WetLitShader = "Hidden/Fpv450/Wet/Lit", WetTerrainShader = "Hidden/Fpv450/Wet/Terrain Lit";
        public const string ExtinctionShader = "Hidden/Fpv450/RainExtinction";

        // The passes URP's Lit editor turns on and off per material, carried over to a twin.
        static readonly string[] Passes = { "ShadowCaster", "DepthOnly", "DepthNormals", "MotionVectors", "Universal2D" };

        static readonly int WetId = Shader.PropertyToID("_Fpv450Wet"), WaterId = Shader.PropertyToID("_Fpv450WetWater"),
            PondId = Shader.PropertyToID("_Fpv450WetPond"), ExtinctionId = Shader.PropertyToID("_Fpv450RainExtinction"),
            AirId = Shader.PropertyToID("_Fpv450RainAir");

        static readonly Dictionary<Material, Material> Twins = new Dictionary<Material, Material>();
        static readonly List<(Renderer renderer, Material[] materials)> Swapped = new List<(Renderer, Material[])>();
        static readonly List<(Terrain terrain, Material template)> Templates = new List<(Terrain, Material)>();
        static readonly List<(Renderer renderer, uint mask)> Marked = new List<(Renderer, uint)>();
        static Shader wetLit, wetTerrain;
        static Material extinction;
        static ExtinctionPass pass;
        static bool hooked;

        /// <summary>What is applied now.</summary>
        public static WetState State { get; private set; }

        /// <summary>The rain's extinction now, 1/m (0 when dry).</summary>
        public static float ExtinctionPerM { get; private set; }

        /// <summary>True while the keyword and the twins are on (rain, or a capture's armed dry reference).</summary>
        public static bool Armed { get; private set; }

        /// <summary>
        /// Applies the flight's rain to the loaded scenes: <paramref name="state"/> the sim's moisture,
        /// <paramref name="extinctionPerM"/> the drop spectrum's (WetOptics.ExtinctionPerM at the flight's rate), and the
        /// renderers whose surfaces the sim calls rigid (they hold standing water in Severe rain). Restores first, so a
        /// call replaces the last; Dry only restores. <paramref name="armedDry"/> (the captures' dry reference only) keeps
        /// everything on with the law at identity, so a probe can be read dry and wet through the same shaders.
        /// </summary>
        public static void Apply(WetState state, float extinctionPerM, IEnumerable<Renderer> hard, bool armedDry = false)
        {
            Restore();
            if (state == WetState.Dry && !armedDry)
                return;
            if (!Load())
                return;

            State = state;
            Armed = true;
            Shader.SetGlobalVector(WetId, new Vector4(state == WetState.Dry ? 0f : 1f, state == WetState.Saturated ? 1f : 0f, 0f, 0f));
            Shader.SetGlobalVector(WaterId, new Vector4((float)WetOptics.EntryTransmission, (float)WetOptics.InternalReflection,
                (float)WetOptics.GrainAbsorption, (float)WetOptics.WaterF0));
            Shader.SetGlobalVector(PondId, new Vector4((float)WetOptics.PondSmoothness, (float)WetOptics.PondCosine, 0f, 0f));
            Shader.EnableKeyword(Keyword);

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                    continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                        TwinRenderer(r);
                    foreach (Terrain t in root.GetComponentsInChildren<Terrain>(true))
                        TwinTerrain(t);
                }
            }
            if (hard != null)
                foreach (Renderer r in hard)
                    if (r != null)
                    {
                        Marked.Add((r, r.renderingLayerMask));
                        r.renderingLayerMask |= 1u << HardLayer;
                    }

            ExtinctionPerM = state == WetState.Dry ? 0f : Mathf.Max(0f, extinctionPerM);
            if (ExtinctionPerM > 0f)
            {
                Shader.SetGlobalVector(ExtinctionId, new Vector4(ExtinctionPerM, 0f, 0f, 0f));
                Shader.SetGlobalVector(AirId, RenderSettings.fogColor.linear);
                RenderPipelineManager.beginCameraRendering += Enqueue;
                hooked = true;
            }
            Debug.Log($"[Look] wet: {state}, {Swapped.Count} renderers and {Templates.Count} terrains on the wet copies, {Marked.Count} hard, " +
                      $"extinction {ExtinctionPerM * 1000f:0.000}/km over the air {RenderSettings.fogColor.linear}" +
                      $"{(state == WetState.Dry ? " (armed dry: the law at identity)" : "")}");
        }

        /// <summary>The captures' probe (WetLaw.hlsl): 0 none, 1 no specular, 2 a white body and no specular.</summary>
        public static void SetProbe(int probe)
        {
            Vector4 v = Shader.GetGlobalVector(WetId);
            v.z = probe;
            Shader.SetGlobalVector(WetId, v);
        }

        /// <summary>Undoes everything <see cref="Apply"/> did: the originals back, the bits cleared, the keyword off.</summary>
        public static void Restore()
        {
            foreach ((Renderer r, Material[] materials) in Swapped)
                if (r != null)
                    r.sharedMaterials = materials;
            foreach ((Terrain t, Material template) in Templates)
                if (t != null)
                    t.materialTemplate = template;
            foreach ((Renderer r, uint mask) in Marked)
                if (r != null)
                    r.renderingLayerMask = mask;
            Swapped.Clear();
            Templates.Clear();
            Marked.Clear();
            foreach (Material twin in Twins.Values)
                Release(twin);
            Twins.Clear();
            if (hooked)
                RenderPipelineManager.beginCameraRendering -= Enqueue;
            hooked = false;
            if (Armed)
            {
                Shader.DisableKeyword(Keyword);
                Shader.SetGlobalVector(WetId, Vector4.zero);
            }
            Armed = false;
            State = WetState.Dry;
            ExtinctionPerM = 0f;
        }

        // The shaders from the player's WetShaders asset (the editor finds them by name without it).
        static bool Load()
        {
            WetShaders assets = WetShaders.Load();
            if (wetLit == null)
                wetLit = assets != null && assets.lit != null ? assets.lit : Shader.Find(WetLitShader);
            if (wetTerrain == null)
                wetTerrain = assets != null && assets.terrain != null ? assets.terrain : Shader.Find(WetTerrainShader);
            if (extinction == null)
            {
                Shader s = assets != null && assets.extinction != null ? assets.extinction : Shader.Find(ExtinctionShader);
                if (s != null)
                    extinction = new Material(s) { name = "RainExtinction", hideFlags = HideFlags.HideAndDontSave };
            }
            if (wetLit != null && wetTerrain != null && extinction != null)
                return true;
            Debug.LogError($"[Look] wet: the wet shaders are missing ({WetLitShader} {wetLit != null}, {WetTerrainShader} {wetTerrain != null}, " +
                           $"{ExtinctionShader} {extinction != null}); the rain draws dry");
            return false;
        }

        static void TwinRenderer(Renderer r)
        {
            Material[] materials = r.sharedMaterials;
            Material[] wet = null;
            for (int i = 0; i < materials.Length; i++)
            {
                Material m = materials[i];
                if (m == null || m.shader == null || m.shader.name != LitShader)
                    continue;
                wet ??= (Material[])materials.Clone();
                wet[i] = Twin(m, wetLit);
            }
            if (wet == null)
                return;
            Swapped.Add((r, materials));
            r.sharedMaterials = wet;
        }

        static void TwinTerrain(Terrain t)
        {
            Material template = t.materialTemplate;
            if (template == null || template.shader == null || template.shader.name != TerrainShader)
                return;
            Templates.Add((t, template));
            t.materialTemplate = Twin(template, wetTerrain);
        }

        // A material's twin on a wet copy: the same properties, keywords, queue and passes.
        static Material Twin(Material source, Shader shader)
        {
            if (Twins.TryGetValue(source, out Material twin))
                return twin;
            twin = new Material(source) { name = source.name + " (wet)", hideFlags = HideFlags.DontSave };
            twin.shader = shader;
            twin.shaderKeywords = source.shaderKeywords;
            twin.renderQueue = source.renderQueue;
            foreach (string p in Passes)
                twin.SetShaderPassEnabled(p, source.GetShaderPassEnabled(p));
            Twins.Add(source, twin);
            return twin;
        }

        static void Release(Object made)
        {
            if (made == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(made);
            else
                Object.DestroyImmediate(made);
        }

        // The extinction on the scene's cameras: never the feed's output (it shows the feed), a preview or a reflection.
        static void Enqueue(ScriptableRenderContext context, Camera camera)
        {
            if (ExtinctionPerM <= 0f || extinction == null || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection
                || camera.GetComponent<FeedOutput>() != null)
                return;
            ScriptableRenderer renderer = camera.GetUniversalAdditionalCameraData()?.scriptableRenderer;
            if (renderer == null)
                return;
            pass ??= new ExtinctionPass();
            pass.Material = extinction;
            renderer.EnqueuePass(pass);
        }

        sealed class ExtinctionPass : ScriptableRenderPass
        {
            public Material Material;

            public ExtinctionPass()
            {
                profilingSampler = new ProfilingSampler("RainExtinction");
                // After the transparents, on the camera's own depth buffer: the depth texture can come from the
                // depth-normals prepass (SSAO's), which the farmland's grass and leaves shaders have no pass for, so
                // their pixels would read the ground behind them. The buffer holds every opaque drawn.
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            sealed class Data
            {
                public TextureHandle Depth;
                public Material Material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.activeDepthTexture.IsValid() || resources.isActiveTargetBackBuffer)
                    return;
                using (var builder = renderGraph.AddRasterRenderPass<Data>("Rain extinction", out var data, profilingSampler))
                {
                    data.Depth = resources.activeDepthTexture;
                    data.Material = Material;
                    builder.UseTexture(data.Depth);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0);
                    builder.SetRenderFunc(static (Data d, RasterGraphContext ctx) =>
                        Blitter.BlitTexture(ctx.cmd, d.Depth, new Vector4(1f, 1f, 0f, 0f), d.Material, 0));
                }
            }
        }
    }
}
