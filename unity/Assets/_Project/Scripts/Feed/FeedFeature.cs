using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Fpv450.Feed
{
    /// <summary>
    /// The analog feed as a URP renderer feature (p4-feed design 1): <c>Shaders/Feed.shader</c>, the Godot feed's
    /// <c>video_feed.gdshader</c> ported, drawn on every camera that carries a <see cref="FeedOutput"/> and on no
    /// other. Three blits: the scene to sRGB code values (skipped for a still that already is), the analog chain at
    /// field resolution (<see cref="FeedSettings.fieldWidth"/> × <see cref="FeedSettings.FieldRows"/>), and the bob
    /// deinterlace with the border into the camera's colour.
    /// </summary>
    public sealed class FeedFeature : ScriptableRendererFeature
    {
        public const int EncodePass = 0, FieldPass = 1, OutputPass = 2;

        [SerializeField] Shader shader;

        Material material;
        FeedPass pass;

        public Shader Shader { get => shader; set => shader = value; }

        public override void Create()
        {
            pass = new FeedPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            if (shader != null && material == null)
                material = CoreUtils.CreateEngineMaterial(shader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || renderingData.cameraData.cameraType == CameraType.Preview)
                return;
            Camera camera = renderingData.cameraData.camera;
            if (!camera.TryGetComponent(out FeedOutput output) || !output.isActiveAndEnabled || output.Scene == null
                || output.Settings == null)
                return;
            pass.Setup(material, output);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
            material = null;
        }

        sealed class FeedPass : ScriptableRenderPass
        {
            static readonly int Gain = Shader.PropertyToID("_Gain"), LinkMargin = Shader.PropertyToID("_LinkMargin"),
                MotorCurrent = Shader.PropertyToID("_MotorCurrent"), Seed = Shader.PropertyToID("_Seed"),
                Field = Shader.PropertyToID("_Field"), N3Amp = Shader.PropertyToID("_N3Amp"),
                N3Roll = Shader.PropertyToID("_N3Roll"), N3Spacing = Shader.PropertyToID("_N3Spacing"),
                N4Amp = Shader.PropertyToID("_N4Amp"), N4Span = Shader.PropertyToID("_N4Span"),
                Degrade = Shader.PropertyToID("_Degrade"), LossState = Shader.PropertyToID("_LossState"),
                LossSplit = Shader.PropertyToID("_LossSplit"), LossTopStrip = Shader.PropertyToID("_LossTopStrip"),
                BlackA = Shader.PropertyToID("_BlackA"), BlackB = Shader.PropertyToID("_BlackB"),
                SnowLevel = Shader.PropertyToID("_SnowLevel"), SnowChroma = Shader.PropertyToID("_SnowChroma"),
                Tell = Shader.PropertyToID("_Tell"), TearAmp = Shader.PropertyToID("_TearAmp"),
                TearTop = Shader.PropertyToID("_TearTop"), GrainBoost = Shader.PropertyToID("_GrainBoost"),
                OsdCodes = Shader.PropertyToID("_OsdCodes"), OsdGlyphs = Shader.PropertyToID("_OsdGlyphs"),
                OsdCells = Shader.PropertyToID("_OsdCells"), OsdCell = Shader.PropertyToID("_OsdCell"),
                OsdOrigin = Shader.PropertyToID("_OsdOrigin"), Lens = Shader.PropertyToID("_Lens"),
                FieldSize = Shader.PropertyToID("_FieldSize"), ShutterTrack = Shader.PropertyToID("_ShutterTrack"),
                Shutter = Shader.PropertyToID("_Shutter");

            Material material;
            FeedOutput output;
            RTHandle scene;
            Texture sceneSource;
            // A render graph import takes a colour or a depth format, never both, so a scene target with a depth buffer
            // (the FPV camera's) comes in by its identifier with its colour's info, and is bound by that identifier.
            bool sceneByIdentifier;
            RenderTargetInfo sceneInfo;

            public FeedPass()
            {
                profilingSampler = new ProfilingSampler("Feed");
                // The output blit must land in URP's intermediate colour, never straight in the back buffer, so the
                // final blit owns the y-flip and the sRGB encode.
                requiresIntermediateTexture = true;
            }

            public void Setup(Material m, FeedOutput o)
            {
                material = m;
                output = o;
                if (sceneSource != o.Scene)
                {
                    scene?.Release();
                    sceneSource = o.Scene;
                    var target = o.Scene as RenderTexture;
                    sceneByIdentifier = target != null && target.depthStencilFormat != GraphicsFormat.None;
                    if (sceneByIdentifier)
                    {
                        scene = RTHandles.Alloc(new RenderTargetIdentifier(target));
                        sceneInfo = new RenderTargetInfo
                        {
                            width = target.width, height = target.height, volumeDepth = 1, msaaSamples = 1,
                            format = target.graphicsFormat,
                        };
                    }
                    else
                        scene = RTHandles.Alloc(o.Scene);
                }
            }

            public void Dispose()
            {
                scene?.Release();
                scene = null;
                sceneSource = null;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                Apply(material, output);

                TextureHandle source = sceneByIdentifier ? renderGraph.ImportTexture(scene, sceneInfo) : renderGraph.ImportTexture(scene);
                bool byIdentifier = sceneByIdentifier;
                if (!output.SceneEncoded)
                {
                    var encoded = renderGraph.CreateTexture(new TextureDesc(output.Scene.width, output.Scene.height)
                    {
                        colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                        name = "_FeedScene",
                    });
                    AddBlit(renderGraph, "Feed encode", source, byIdentifier, encoded, EncodePass);
                    source = encoded;
                    byIdentifier = false;
                }

                var field = renderGraph.CreateTexture(new TextureDesc(output.Settings.fieldWidth, FeedSettings.FieldRows)
                {
                    colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                    name = "_FeedField",
                });
                AddBlit(renderGraph, "Feed field", source, byIdentifier, field, FieldPass);
                AddBlit(renderGraph, "Feed output", field, false, resources.activeColorTexture, OutputPass);
            }

            sealed class BlitData
            {
                public TextureHandle Source;
                public bool ByIdentifier;
                public Material Material;
                public int Pass;
            }

            /// A full-screen blit of one of the shader's passes. RenderGraph's own AddBlitPass reads the source's
            /// descriptor, which an imported still (a Texture2D) does not have, so this is the plain raster pass.
            void AddBlit(RenderGraph renderGraph, string name, TextureHandle source, bool byIdentifier, TextureHandle destination,
                int shaderPass)
            {
                using (var builder = renderGraph.AddRasterRenderPass<BlitData>(name, out var data, profilingSampler))
                {
                    data.Source = source;
                    data.ByIdentifier = byIdentifier;
                    data.Material = material;
                    data.Pass = shaderPass;
                    builder.UseTexture(source);
                    builder.SetRenderAttachment(destination, 0);
                    builder.SetRenderFunc(static (BlitData d, RasterGraphContext ctx) =>
                    {
                        var scaleBias = new Vector4(1f, 1f, 0f, 0f);
                        if (d.ByIdentifier)
                            Blitter.BlitTexture(ctx.cmd, (RenderTargetIdentifier)(RTHandle)d.Source, scaleBias, d.Material, d.Pass);
                        else
                            Blitter.BlitTexture(ctx.cmd, d.Source, scaleBias, d.Material, d.Pass);
                    });
                }
            }

            static void Apply(Material m, FeedOutput o)
            {
                FeedUniforms u = o.Uniforms;
                m.SetFloat(Gain, u.Gain);
                m.SetFloat(LinkMargin, u.LinkMargin);
                m.SetFloat(MotorCurrent, u.MotorCurrent);
                m.SetFloat(Seed, u.Seed);
                m.SetFloat(Field, u.Field);
                m.SetFloat(N3Amp, u.N3Amp);
                m.SetFloat(N3Roll, u.N3Roll);
                m.SetFloat(N3Spacing, u.N3Spacing);
                m.SetFloat(N4Amp, u.N4Amp);
                m.SetFloat(N4Span, u.N4Span);
                m.SetFloat(Degrade, u.Degrade);
                m.SetFloat(LossState, u.LossState);
                m.SetFloat(LossSplit, u.LossSplit);
                m.SetFloat(LossTopStrip, u.LossTopStrip);
                m.SetFloat(BlackA, u.BlackA);
                m.SetFloat(BlackB, u.BlackB);
                m.SetFloat(SnowLevel, u.SnowLevel);
                m.SetFloat(SnowChroma, u.SnowChroma);
                m.SetFloat(Tell, u.Tell);
                m.SetFloat(TearAmp, u.TearAmp);
                m.SetFloat(TearTop, u.TearTop);
                m.SetFloat(GrainBoost, u.GrainBoost);
                m.SetTexture(OsdCodes, o.OsdCodes != null ? o.OsdCodes : Texture2D.blackTexture);
                m.SetTexture(OsdGlyphs, o.OsdGlyphs != null ? o.OsdGlyphs : Texture2D.blackTexture);
                m.SetFloat(OsdCells, o.OsdCells);
                m.SetVector(OsdCell, o.OsdCell);
                m.SetVector(OsdOrigin, o.OsdOrigin);
                m.SetVector(Lens, o.Settings.LensVector);
                m.SetVector(FieldSize, new Vector4(o.Settings.fieldWidth, FeedSettings.FieldRows, 0f, 0f));
                // The camera's read (p7e-camera-feel): without a track, the old read.
                bool track = o.ShutterTrack != null && o.Shutter.x > 0.5f;
                m.SetTexture(ShutterTrack, track ? o.ShutterTrack : Texture2D.blackTexture);
                m.SetVector(Shutter, track ? o.Shutter : Vector4.zero);
            }
        }
    }
}
