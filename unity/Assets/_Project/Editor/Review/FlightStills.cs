using System;
using System.IO;
using System.Linq;
using Fpv450.Feed;
using Fpv450.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Fpv450
{
    /// <summary>
    /// Stills of S04's straw from the flight camera (D-081): in the built test range scene, by the day and overcast
    /// presets, the FPV pinhole (FeedSettings: its focal length and 16:9 at 1920 across, no lens, no feed, no post, no
    /// AA, as the flight renders the scene) over a verge patch, looking 45° down at it from 80, 30, 10 and 4 m up; an
    /// approach 1.5 m up at 9, 7.5, 6.5 and 5 m from the patch's LOD centre, as Unity picks the LODs, each with the share
    /// of LOD0 in what is drawn against the patch forced to LOD0 and to LOD1 (the cross-fade's band, read off the render); and the two switch
    /// pairs: 1.5 m up at 8 m and at 60 m from the patch's LOD centre, every S04 patch forced to the
    /// outgoing LOD and then to the incoming one. Per pair it prints the ground's mean linear luminance in each and their
    /// difference, and the mean of |a - b| over 16 px blocks of the ground, both as a percentage of the outgoing mean:
    /// what a pop would show. PNGs go to the folder after <c>-out</c>, which belongs under the ignored
    /// <c>reference/_compare/</c>. Batchmode with graphics:
    /// <c>-batchmode -quit -executeMethod Fpv450.FlightStills.Capture -out &lt;folder&gt;</c>; exit code 1 on any error.
    /// </summary>
    public static class FlightStills
    {
        const int Width = 1920, Height = 1080, Block = 16;
        static readonly Vector3 Target = new Vector3(10f, 0f, -13.355f); // the south verge's patch nearest the posadka's middle
        static readonly float[] HeightsM = { 80f, 30f, 10f, 4f };
        static readonly float[] ApproachM = { 9f, 7.5f, 6.5f, 5f }; // unforced, across LOD0's band
        static readonly string[] Presets = { "day", "overcast" };

        public static void Capture()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-out");
                if (at < 0 || at + 1 >= args.Length)
                    throw new ArgumentException("usage: -executeMethod Fpv450.FlightStills.Capture -out <folder>");
                Run(Path.GetFullPath(args[at + 1]));
            }
            catch (Exception e)
            {
                Debug.LogError($"[FlightStills] FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
        }

        static void Run(string folder)
        {
            Directory.CreateDirectory(folder);
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.OpenScene(TestRangeScene.ScenePath, OpenSceneMode.Single);
            LookSetup look = Object.FindFirstObjectByType<LookSetup>() ?? throw new InvalidOperationException("the test range has no LookSetup");
            LODGroup[] patches = Object.FindObjectsByType<AssetPhysicsMeta>(FindObjectsSortMode.None)
                .Where(m => m.id == "S04").Select(m => m.GetComponent<LODGroup>()).ToArray();
            LODGroup target = patches.OrderBy(g => (g.transform.position - Target).sqrMagnitude).First();
            Vector3 centre = target.transform.TransformPoint(target.localReferencePoint);
            FeedSettings feed = FeedSettings.Load() ?? throw new InvalidOperationException("no Resources/FeedSettings");
            var camera = new GameObject("FlightStills").AddComponent<Camera>();
            camera.fieldOfView = feed.VerticalFovDeg(1f);
            camera.aspect = (float)Width / Height;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 3000f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            camera.enabled = false;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            look.viewCamera = camera;
            var targetTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { antiAliasing = Mathf.Max(1, UniversalRenderPipeline.asset.msaaSampleCount) };
            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            Debug.Log($"[FlightStills] {patches.Length} S04 patches, the target at {target.transform.position} (LOD centre {centre}), " +
                      $"fade {target.fadeMode}, FPV pinhole {camera.fieldOfView:0.00}° vertical at {Width}x{Height}, MSAA {targetTexture.antiAliasing}, " +
                      $"GPU {SystemInfo.graphicsDeviceName}");
            try
            {
                foreach (string preset in Presets)
                {
                    int index = Enumerable.Range(0, look.PresetCount).First(i => look.presets[i].name.Contains(preset));
                    look.Select(index);
                    Render(camera, targetTexture, readback); // warm-up
                    foreach (float height in HeightsM)
                    {
                        Aim(camera, centre + new Vector3(0f, height, -height), centre, look);
                        Save(Render(camera, targetTexture, readback), Path.Combine(folder, $"{preset}_{height:0}m.png"));
                    }
                    foreach (float distance in ApproachM)
                    {
                        Aim(camera, Eye(centre, distance), centre + new Vector3(0f, 0f, 6f), look);
                        Texture2D shot = Render(camera, targetTexture, readback);
                        Color32[] drawn = shot.GetPixels32();
                        Save(shot, Path.Combine(folder, $"{preset}_approach{distance:0.0}m.png"));
                        var forced = new Color32[2][];
                        for (int k = 0; k < 2; k++)
                        {
                            target.ForceLOD(k);
                            forced[k] = Render(camera, targetTexture, readback).GetPixels32();
                        }
                        target.ForceLOD(-1);
                        Debug.Log($"[FlightStills] {preset} approach {distance:0.0} m: LOD0's share of what is drawn {Share(drawn, forced[0], forced[1]):0.00} " +
                                  "(the sum of |drawn - LOD1| over that of |LOD0 - LOD1|: 0 LOD1 alone, 1 LOD0 alone, between them the dither's share)");
                    }
                    foreach ((float distance, int outgoing) in new[] { (8f, 0), (60f, 1) })
                    {
                        Aim(camera, Eye(centre, distance), centre + new Vector3(0f, 0f, 6f), look);
                        Color32[][] shots = new Color32[2][];
                        for (int k = 0; k < 2; k++)
                        {
                            foreach (LODGroup g in patches)
                                g.ForceLOD(outgoing + k);
                            Texture2D shot = Render(camera, targetTexture, readback);
                            shots[k] = shot.GetPixels32();
                            Save(shot, Path.Combine(folder, $"{preset}_switch{distance:0}m_LOD{outgoing + k}.png"));
                        }
                        foreach (LODGroup g in patches)
                            g.ForceLOD(-1);
                        Debug.Log($"[FlightStills] {preset} switch {distance:0} m, LOD{outgoing} to LOD{outgoing + 1}: {Compare(shots[0], shots[1])}");
                    }
                }
            }
            finally
            {
                foreach (LODGroup g in patches)
                    g.ForceLOD(-1);
                Object.DestroyImmediate(camera.gameObject);
                targetTexture.Release();
            }
        }

        /// <summary>1.5 m up, <paramref name="distance"/> from the patch's LOD centre, south of it.</summary>
        static Vector3 Eye(Vector3 centre, float distance) =>
            new Vector3(centre.x, 1.5f, centre.z - Mathf.Sqrt(distance * distance - (1.5f - centre.y) * (1.5f - centre.y)));

        static void Aim(Camera camera, Vector3 eye, Vector3 at, LookSetup look)
        {
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye, Vector3.up));
            look.UpdateFog(camera);
        }

        /// <summary>The ground's mean luminance in each (rows below the frame's middle) and the mean |a - b| over its 16 px blocks.</summary>
        static string Compare(Color32[] a, Color32[] b)
        {
            int rows = Height / 2 / Block, columns = Width / Block;
            double meanA = 0, meanB = 0, blocks = 0;
            for (int by = 0; by < rows; by++)
                for (int bx = 0; bx < columns; bx++)
                {
                    double sa = 0, sb = 0;
                    for (int y = by * Block; y < (by + 1) * Block; y++)
                        for (int x = bx * Block; x < (bx + 1) * Block; x++)
                        {
                            sa += Luminance(a[y * Width + x]);
                            sb += Luminance(b[y * Width + x]);
                        }
                    sa /= Block * Block;
                    sb /= Block * Block;
                    meanA += sa;
                    meanB += sb;
                    blocks += Math.Abs(sa - sb);
                }
            int n = rows * columns;
            meanA /= n;
            meanB /= n;
            return $"ground mean Y {meanA:0.0000} then {meanB:0.0000} ({100 * (meanB / meanA - 1):+0.0;-0.0} %), " +
                   $"mean |difference| over {Block} px blocks {100 * blocks / n / meanA:0.0} % of the outgoing mean";
        }

        static double Share(Color32[] drawn, Color32[] lod0, Color32[] lod1)
        {
            double over = 0, under = 0;
            for (int i = 0; i < drawn.Length; i++)
            {
                over += Math.Abs(Luminance(drawn[i]) - Luminance(lod1[i]));
                under += Math.Abs(Luminance(lod0[i]) - Luminance(lod1[i]));
            }
            return under > 0 ? over / under : double.NaN;
        }

        static double Luminance(Color32 c) =>
            0.2126 * Mathf.GammaToLinearSpace(c.r / 255f) + 0.7152 * Mathf.GammaToLinearSpace(c.g / 255f) + 0.0722 * Mathf.GammaToLinearSpace(c.b / 255f);

        static Texture2D Render(Camera camera, RenderTexture target, Texture2D readback)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new InvalidOperationException("the pipeline takes no render request");
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            readback.Apply(false);
            RenderTexture.active = null;
            return readback;
        }

        static void Save(Texture2D texture, string path)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Debug.Log($"[FlightStills] wrote {path}");
        }
    }
}
