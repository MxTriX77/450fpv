using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Fpv450.Sim.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Fpv450.App
{
    /// <summary>
    /// The drawn terrain against the sim's heightfield near the drone (w3-farmland-ground 3.1, WLD-11: what is drawn is what
    /// is touched), measured from the depth Unity draws. A camera with the FPV camera's lens, field of view and render size
    /// (so Unity's terrain picks the same level of detail for each patch: its error is in that camera's pixels) draws the
    /// terrains alone, their trees and grass off, into a depth target from the lens, looking along the lens and straight
    /// down. Each pixel's point goes back to the world through the GPU's own projection, and its height is compared with
    /// the sim's SampleGround there, binned by its horizontal distance from the lens. The comparison runs off the frame.
    /// </summary>
    public static class TerrainParity
    {
        /// <summary>The distance bands from the lens, m: what the drone touches, then what the pilot sees close.</summary>
        public static readonly double[] Bands = { 2, 10, 30 };

        /// <summary>One view's figures per band: pixels on the terrain, the largest |drawn − sim| and the mean drawn − sim, m.</summary>
        public sealed class View
        {
            public string Name;
            public int[] Pixels = new int[Bands.Length];
            public double[] Max = new double[Bands.Length], Mean = new double[Bands.Length];
            public bool Flipped; // the rows' order that matched (the GPU's convention), for the record

            /// <summary>One pixel's angle of the render, rad (its vertical field of view over its rows).</summary>
            public double PixelRad;

            /// <summary>One pixel's size at a band's outer edge, m: Unity's pixel error 1 keeps the drawn terrain within about
            /// that of its heightmap there.</summary>
            public double PixelAt(int band) => Bands[band] * PixelRad;

            public override string ToString()
            {
                var b = new StringBuilder(Name);
                for (int i = 0; i < Bands.Length; i++)
                    b.Append(i == 0 ? ": " : "; ").Append("≤ ").Append(Bands[i]).Append(" m ").Append(Pixels[i]).Append(" px, max ")
                        .Append(Pixels[i] > 0 ? (Max[i] * 1e3).ToString("0.000") + " mm (" + (Max[i] / PixelAt(i)).ToString("0.00") + " px at " + Bands[i] + " m)" : "-").Append(", mean ")
                        .Append(Pixels[i] > 0 ? (Mean[i] * 1e3).ToString("0.000") + " mm" : "-");
                return b.ToString();
            }
        }

        /// <summary>
        /// Draws the terrains from like's pose with its lens (along it, and straight down), with each setting's pixel error per
        /// terrain, and compares them with world's ground off the frame. The terrains are left as they were.
        /// </summary>
        public static Task<List<View>> Measure(Camera like, IWorldQuery world, string where, params (string Name, Func<Terrain, float> Error)[] settings)
        {
            Terrain[] terrains = Terrain.activeTerrains;
            int layer = FreeLayer();
            int w = like.targetTexture != null ? like.targetTexture.width : like.pixelWidth;
            int h = like.targetTexture != null ? like.targetTexture.height : like.pixelHeight;
            var go = new GameObject("TerrainParity") { hideFlags = HideFlags.HideAndDontSave };
            var probe = go.AddComponent<Camera>();
            probe.CopyFrom(like);
            probe.enabled = false;
            probe.cullingMask = 1 << layer;
            probe.clearFlags = CameraClearFlags.SolidColor;
            UniversalAdditionalCameraData data = probe.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            var depth = new RenderTexture(w, h, 32, RenderTextureFormat.Depth) { name = "TerrainParityDepth" };
            var copy = new RenderTexture(w, h, 0, RenderTextureFormat.RFloat) { name = "TerrainParityCopy" };
            depth.Create();
            copy.Create();
            probe.targetTexture = depth;

            var saved = new (int Layer, bool Foliage, float Error)[terrains.Length];
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain t = terrains[i];
                saved[i] = (t.gameObject.layer, t.drawTreesAndFoliage, t.heightmapPixelError);
                t.gameObject.layer = layer;
                t.drawTreesAndFoliage = false;
            }
            var shots = new List<(string Name, float[] Depth, Matrix4x4 Inverse, Vector3 Eye, Quaternion Turn)>();
            try
            {
                Transform lens = like.transform;
                foreach ((string setting, Func<Terrain, float> error) in settings)
                    foreach (bool down in new[] { false, true })
                    {
                        for (int i = 0; i < terrains.Length; i++) terrains[i].heightmapPixelError = error(terrains[i]);
                        Quaternion turn = down ? Quaternion.LookRotation(Vector3.down, lens.forward) : lens.rotation;
                        go.transform.SetPositionAndRotation(lens.position, turn);
                        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = depth };
                        if (!RenderPipeline.SupportsRenderRequest(probe, request))
                            throw new InvalidOperationException("the pipeline takes no single-camera request");
                        RenderPipeline.SubmitRenderRequest(probe, request);
                        Graphics.Blit(depth, copy);
                        AsyncGPUReadbackRequest read = AsyncGPUReadback.Request(copy, 0, TextureFormat.RFloat);
                        read.WaitForCompletion();
                        if (read.hasError) throw new InvalidOperationException("the depth's readback failed");
                        Matrix4x4 gpu = GL.GetGPUProjectionMatrix(probe.projectionMatrix, true);
                        shots.Add((where + (down ? ", straight down" : ", along the lens") + ", pixel error " + setting,
                            read.GetData<float>().ToArray(), gpu.inverse, lens.position, turn));
                    }
            }
            finally
            {
                for (int i = 0; i < terrains.Length; i++)
                {
                    Terrain t = terrains[i];
                    t.gameObject.layer = saved[i].Layer;
                    t.drawTreesAndFoliage = saved[i].Foliage;
                    t.heightmapPixelError = saved[i].Error;
                }
                probe.targetTexture = null;
                depth.Release();
                copy.Release();
                UnityEngine.Object.Destroy(depth);
                UnityEngine.Object.Destroy(copy);
                UnityEngine.Object.Destroy(go);
            }
            double pixelRad = like.fieldOfView * Math.PI / 180 / h;
            return Task.Run(() => shots.ConvertAll(s => Compare(s.Name, s.Depth, w, h, s.Inverse, s.Eye, s.Turn, world, pixelRad)));
        }

        // Each pixel's point from its depth through the GPU's projection, in the lens's frame, then the world's in double;
        // its height against the sim's. The rows' order (D3D's top first, or the flip) is the one whose points lie on the sim's
        // ground: the other puts each depth on another pixel's ray, metres off.
        static View Compare(string name, float[] depth, int w, int h, Matrix4x4 inverse, Vector3 eye, Quaternion turn, IWorldQuery world, double pixelRad)
        {
            View best = null;
            foreach (bool flip in new[] { false, true })
            {
                var v = new View { Name = name, Flipped = flip, PixelRad = pixelRad };
                var sums = new double[Bands.Length];
                var at = new XZ[1];
                var ground = new GroundSample[1];
                for (int r = 0; r < h; r++)
                    for (int c = 0; c < w; c++)
                    {
                        float d = depth[r * w + c];
                        if (d <= 0f || d >= 1f) continue; // the far plane's clear, or the near plane
                        float ny = flip ? 2f * (r + 0.5f) / h - 1f : 1f - 2f * (r + 0.5f) / h;
                        Vector4 p = inverse * new Vector4(2f * (c + 0.5f) / w - 1f, ny, d, 1f);
                        Vector3 local = new Vector3(p.x / p.w, p.y / p.w, -p.z / p.w); // the view's −z is the lens's forward
                        Vector3 offset = turn * local;
                        double x = eye.x + (double)offset.x, y = eye.y + (double)offset.y, z = eye.z + (double)offset.z;
                        double horizontal = Math.Sqrt((double)offset.x * offset.x + (double)offset.z * offset.z);
                        int band = 0;
                        while (band < Bands.Length && horizontal > Bands[band]) band++;
                        if (band == Bands.Length) continue;
                        at[0] = new XZ(x, z);
                        world.SampleGround(at, ground);
                        double diff = y - ground[0].Height;
                        for (int b = band; b < Bands.Length; b++)
                        {
                            v.Pixels[b]++;
                            v.Max[b] = Math.Max(v.Max[b], Math.Abs(diff));
                            sums[b] += diff;
                        }
                    }
                for (int b = 0; b < Bands.Length; b++) v.Mean[b] = v.Pixels[b] > 0 ? sums[b] / v.Pixels[b] : 0;
                int last = Bands.Length - 1;
                if (best == null || v.Pixels[last] > 0 && (best.Pixels[last] == 0 || Math.Abs(v.Mean[last]) + v.Max[last] < Math.Abs(best.Mean[last]) + best.Max[last]))
                    best = v;
            }
            return best;
        }

        // The highest layer no one named: the terrains sit on it for the probe's draw alone.
        static int FreeLayer()
        {
            for (int i = 31; i >= 8; i--)
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                    return i;
            throw new InvalidOperationException("no free layer for the terrain probe");
        }
    }
}
