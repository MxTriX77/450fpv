using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Fpv450.App
{
    /// <summary>
    /// The pieces' drawing (w16-plants-in-the-air 1.4; Shaders/PlantPieces.shader): each frame the newest tick's pieces at the
    /// frame's flight time, a tick behind it as the wash is drawn (PlantField), in the flight's camera, so the feed takes them with
    /// the world (its softness, its noise). One pass on that camera before its post-processing (injected at beginCameraRendering, so
    /// a still's camera.Render draws them as the player does): the cut pieces and the bits at the render's resolution, tested against
    /// its depth; then the dust at a quarter of it (DustDivisor), composited over the scene: the feed's field resolves about
    /// 720 × 288, a quarter of the render's 2550 × 1435, so the dust loses nothing the feed shows, at a sixteenth of the fill (a veil
    /// is many full-screen puffs). Drawing only.
    /// </summary>
    public sealed class PlantPiecesView : IDisposable
    {
        public const string MaterialResource = "PlantPieces";

        /// <summary>The dust target's size over the camera's, each side (numerical: the feed's field resolution, above).</summary>
        public const int DustDivisor = 4;

        public const int PiecesPass = 0, DustPass = 1, CompositePass = 2;

        static readonly int PiecesId = Shader.PropertyToID("_Pieces"), TimeId = Shader.PropertyToID("_PieceTime"),
            OffsetId = Shader.PropertyToID("_PieceOffset"), TargetId = Shader.PropertyToID("_DustTarget");

        readonly Material _material;
        readonly GraphicsBuffer _buffer;
        readonly Pass _pass = new Pass();
        Camera _camera;

        /// <summary>The material's missing (no Resources/PlantPieces): nothing is drawn.</summary>
        public bool Ready => _material != null;

        /// <summary>The flight time (s) the last frame drew the pieces at, NaN before the first; the pieces and puffs it drew.</summary>
        public double Shown { get; private set; } = double.NaN;
        public int ShownPieces { get; private set; }
        public int ShownDust { get; private set; }

        /// <summary>The passes recorded since the view began: the pieces' and the dust's (a still's or a test's check).</summary>
        public int PiecesRecorded => _pass.PiecesRecorded;
        public int DustRecorded => _pass.DustRecorded;

        public PlantPiecesView()
        {
            Material m = Resources.Load<Material>(MaterialResource);
            if (m == null)
            {
                Debug.LogError("[Pieces] no Resources/" + MaterialResource + ": the pieces in the air are not drawn");
                return;
            }
            _material = new Material(m) { name = "Plant pieces (flight)", hideFlags = HideFlags.DontSave };
            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, PlantPieces.Capacity, PlantPieces.Stride * sizeof(float));
            _material.SetBuffer(PiecesId, _buffer);
            _pass.Material = _material;
            RenderPipelineManager.beginCameraRendering += Enqueue;
        }

        /// <summary>
        /// The frame (render thread): the pieces at flight time t (s) in the camera, swept over its exposure (s). The newest tick at
        /// or before t is drawn a tick behind t, between its sub-steps (as the wash: never still while the ticks keep up).
        /// </summary>
        public void Draw(PlantPieces pieces, double t, Camera camera, double exposureS)
        {
            _pass.Pieces = _pass.Dust = false;
            if (_material == null || pieces == null || camera == null) return;
            PlantPieces.Snapshot s = pieces.Take(t, out bool changed);
            if (s == null) return;
            if (changed) _buffer.SetData(s.Data);
            double drawn = Math.Max(s.T0, Math.Min(s.T0 + s.TickSeconds, t - s.TickSeconds));
            Shown = drawn;
            ShownPieces = s.Pieces;
            ShownDust = s.Dust;
            _material.SetVector(TimeId, new Vector4((float)(drawn - s.T0), (float)s.TickSeconds, (float)(s.TickSeconds / PlantPieces.SubSteps), (float)exposureS));
            _material.SetInt(OffsetId, PlantPieces.CutFirst);
            _camera = camera;
            _pass.Pieces = s.Pieces > 0;
            _pass.Dust = s.Dust > 0;
        }

        // The pass on the flight's camera, the frames it draws pieces or dust.
        void Enqueue(ScriptableRenderContext context, Camera camera)
        {
            if (!(_pass.Pieces || _pass.Dust) || camera != _camera || _material == null) return;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data == null || data.scriptableRenderer == null) return;
            _pass.ConfigureInput(_pass.Dust ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None); // the depth texture for the dust alone
            data.scriptableRenderer.EnqueuePass(_pass);
        }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= Enqueue;
            _buffer?.Release();
            CoreUtils.Destroy(_material);
        }

        // The pieces over the camera's colour against its depth (pass 0); the quarter target cleared, every puff added to it (pass 1,
        // reading the camera's depth texture), then over the camera's colour (pass 2).
        sealed class Pass : ScriptableRenderPass
        {
            public Material Material;
            public bool Pieces, Dust;
            public int PiecesRecorded, DustRecorded;

            sealed class DrawData
            {
                public Material Material;
                public Vector4 Target;
            }

            sealed class PiecesData
            {
                public Material Material;
            }

            sealed class CompositeData
            {
                public Material Material;
                public TextureHandle Dust;
            }

            public Pass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalCameraData camera = frameData.Get<UniversalCameraData>();
                if (Pieces)
                    using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Plant pieces", out PiecesData data))
                    {
                        PiecesRecorded++;
                        builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                        builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                        builder.AllowGlobalStateModification(true);
                        data.Material = Material;
                        builder.SetRenderFunc((PiecesData d, RasterGraphContext context) =>
                            context.cmd.DrawProcedural(Matrix4x4.identity, d.Material, PiecesPass, MeshTopology.Triangles, 6, PlantPieces.Capacity - PlantPieces.CutFirst));
                    }
                if (!Dust || !resources.cameraDepthTexture.IsValid()) return;
                DustRecorded++;
                RenderTextureDescriptor target = camera.cameraTargetDescriptor;
                int w = Math.Max(1, target.width / DustDivisor), h = Math.Max(1, target.height / DustDivisor);
                var desc = new TextureDesc(w, h)
                {
                    format = GraphicsFormat.R16G16B16A16_SFloat, clearBuffer = true, clearColor = Color.clear, name = "Plant dust",
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                };
                TextureHandle dust = renderGraph.CreateTexture(desc);
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Plant dust", out DrawData data))
                {
                    builder.SetRenderAttachment(dust, 0, AccessFlags.Write);
                    builder.UseTexture(resources.cameraDepthTexture);
                    builder.AllowGlobalStateModification(true);
                    data.Material = Material;
                    data.Target = new Vector4(w, h, 1f / w, 1f / h);
                    builder.SetRenderFunc((DrawData d, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalVector(TargetId, d.Target);
                        context.cmd.DrawProcedural(Matrix4x4.identity, d.Material, DustPass, MeshTopology.Triangles, 6, PlantPieces.DustSlots);
                    });
                }
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Plant dust over the scene", out CompositeData data))
                {
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.UseTexture(dust);
                    data.Material = Material;
                    data.Dust = dust;
                    builder.SetRenderFunc((CompositeData d, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, d.Dust, new Vector4(1, 1, 0, 0), d.Material, CompositePass));
                }
            }
        }
    }
}
