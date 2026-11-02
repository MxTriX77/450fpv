using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Fpv450.Feed;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Fpv450
{
    /// <summary>
    /// The look setup's evidence (p1-g1-look-lock-gallery task 1.1): under each light preset, the S04 patch from its
    /// viewpoint (1.5 m over the mat, 35° down, design 3) and three V01_L from 200 m (2 m up, level, design 1), at
    /// 1920 x 1080 through the game's pipeline (MSAA as the URP asset has it, no post), written as PNGs to the folder
    /// after <c>-out</c>. It also prints, per preset, a horizontal 0.18 grey rendered against the model's
    /// horizontal illuminance (a card without specular or reflection, and URP/Lit at smoothness 0.05), the belt's crowns against the sky just above them (encoded BT.601 luma, as the
    /// reference notes measure) at the asset's MSAA, at 1 and with SMAA, a mirror ball's top and bottom (the
    /// reflection's orientation), and what MSAA, the fog, the sky and SMAA cost (<see cref="Cost"/>).
    /// <c>-batchmode -quit -executeMethod Fpv450.LookCapture.Run -out &lt;folder&gt;</c>; exit code 1 on any error.
    /// </summary>
    public static class LookCapture
    {
        const int Width = 1920, Height = 1080;
        const float VerticalFovDeg = 60f;
        const float GreyAlbedo = 0.18f;
        static readonly Vector3 PatchAt = new Vector3(-100f, 0f, -100f);
        static readonly Vector3 GreyAt = new Vector3(-400f, 0f, -400f);
        static readonly Vector3 DiffuseAt = new Vector3(-400f, 0f, -380f);
        static readonly Vector3 BallAt = new Vector3(-300f, 1f, -400f);
        static readonly Vector3 BeltCamera = new Vector3(0f, 2f, 0f);
        static readonly float[] TreeX = { -6.5f, 0f, 6.5f };
        const float TreeZ = 200f;

        public static void Run()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-out");
                if (at < 0 || at + 1 >= args.Length)
                    throw new ArgumentException("usage: -executeMethod Fpv450.LookCapture.Run -out <folder>");
                Capture(Path.GetFullPath(args[at + 1]));
            }
            catch (Exception e)
            {
                Debug.LogError($"[LookCapture] FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
        }

        static void Capture(string folder)
        {
            Directory.CreateDirectory(folder);
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UniversalRenderPipelineAsset pipeline = UniversalRenderPipeline.asset ?? throw new InvalidOperationException("no URP asset is active");
            int msaa = Mathf.Max(1, pipeline.msaaSampleCount);
            Debug.Log($"[LookCapture] GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), quality {QualitySettings.names[QualitySettings.GetQualityLevel()]} ({pipeline.name}), " +
                      $"LOD bias {QualitySettings.lodBias:0.##}, MSAA {msaa}, colour space {QualitySettings.activeColorSpace}, {Width}x{Height}, FOV {VerticalFovDeg:0}° vertical");

            SkyModel model = SkyModel.FromJson(File.ReadAllText(LookSetup.SkyModelPath), LookSetup.SkyModelPath);
            Material floor = Lit(new Color(model.ground_albedo_linear[0], model.ground_albedo_linear[1], model.ground_albedo_linear[2]), 0.1f);
            Quad("Floor", Vector3.zero, 2000f, floor);
            Quad("Grey", GreyAt + Vector3.up * 0.01f, 4f, Lit(new Color(GreyAlbedo, GreyAlbedo, GreyAlbedo), 0.05f));
            Material diffuse = Lit(new Color(GreyAlbedo, GreyAlbedo, GreyAlbedo), 0f);
            diffuse.SetFloat("_SpecularHighlights", 0f);
            diffuse.SetFloat("_EnvironmentReflections", 0f);
            diffuse.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            diffuse.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            Quad("GreyDiffuse", DiffuseAt + Vector3.up * 0.01f, 4f, diffuse);
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.position = BallAt;
            Material mirror = Lit(Color.white, 1f);
            mirror.SetFloat("_Metallic", 1f);
            ball.GetComponent<MeshRenderer>().sharedMaterial = mirror;

            GameObject patch = Place("Assets/_Project/Prefabs/surfaces/S04_patch.prefab", "Assets/_Project/Art/Models/surfaces/S04/S04_patch.assetmeta.json", PatchAt, 0f);
            var trees = new GameObject("Belt");
            for (int i = 0; i < TreeX.Length; i++)
                Place("Assets/_Project/Prefabs/vegetation/V01_L.prefab", null, new Vector3(TreeX[i], 0f, TreeZ), 120f * i).transform.SetParent(trees.transform, true);

            var look = new GameObject("Look").AddComponent<LookSetup>();
            look.skyModel = AssetDatabase.LoadAssetAtPath<TextAsset>(LookSetup.SkyModelPath);
            look.presets = LookSetup.PresetPaths.Select(path => AssetDatabase.LoadAssetAtPath<TextAsset>(path)).ToArray();
            look.skyShader = AssetDatabase.LoadAssetAtPath<Shader>(LookSetup.SkyShaderPath);

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.fieldOfView = VerticalFovDeg;
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

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = msaa };
            var single = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            Vector3 patchEye = patch.transform.position + Vector3.up * 1.5f;
            float down = 35f * Mathf.Deg2Rad;
            try
            {
                for (int i = 0; i < look.PresetCount; i++)
                {
                    look.Select(i);
                    string name = look.Active.name;

                    Aim(camera, patchEye, new Vector3(Mathf.Cos(down), -Mathf.Sin(down), 0f), look);
                    Render(camera, target, readback); // warm-up
                    Save(Render(camera, target, readback), Path.Combine(folder, $"look_{name}_s04.png"));

                    Aim(camera, BeltCamera, Vector3.forward, look);
                    Render(camera, target, readback);
                    Color32[] belt = Render(camera, target, readback).GetPixels32();
                    Save(readback, Path.Combine(folder, $"look_{name}_belt200.png"));
                    trees.SetActive(false);
                    Color32[] open = Render(camera, target, readback).GetPixels32();
                    trees.SetActive(true);
                    Color32[] belt1 = Render(camera, single, readback).GetPixels32();
                    trees.SetActive(false);
                    Color32[] open1 = Render(camera, single, readback).GetPixels32();
                    trees.SetActive(true);
                    Band(name, $"MSAA {msaa}", belt, open);
                    Band(name, "MSAA 1", belt1, open1);
                    Smaa(data, true);
                    Color32[] beltSmaa = Render(camera, single, readback).GetPixels32();
                    trees.SetActive(false);
                    Color32[] openSmaa = Render(camera, single, readback).GetPixels32();
                    trees.SetActive(true);
                    Smaa(data, false);
                    Band(name, "MSAA 1 + SMAA high (post on)", beltSmaa, openSmaa);

                    foreach ((string card, Vector3 at) in new[] { ("diffuse (no specular, no reflection)", DiffuseAt), ("URP/Lit smoothness 0.05", GreyAt) })
                    {
                        Aim(camera, at + Vector3.up * 2f, Vector3.down, look, 10f);
                        Color32[] grey = Render(camera, target, readback).GetPixels32();
                        float rendered = MeanLinear(grey, Width / 2 - 128, Height / 2 - 128, 256, 256) / GreyAlbedo;
                        Debug.Log($"[LookCapture] {name}: horizontal 0.18 grey, {card}: rendered sRGB {255f * Mathf.LinearToGammaSpace(GreyAlbedo * rendered):0.0}, luminance over 0.18 {rendered:0.000} against the modelled illuminance {look.State.HorizontalIlluminance:0.000} ({100f * (rendered / look.State.HorizontalIlluminance - 1f):+0.0;-0.0} %)");
                    }

                    Aim(camera, BallAt + new Vector3(0f, 0f, -4f), Vector3.forward, look, 20f);
                    Color32[] mirrorPixels = Render(camera, target, readback).GetPixels32();
                    float radiusPx = 0.5f / (4f * Mathf.Tan(10f * Mathf.Deg2Rad)) * (Height / 2f);
                    Color32 top = Mean(mirrorPixels, Width / 2 - 8, (int)(Height / 2 + 0.8f * radiusPx) - 8, 16, 16);
                    Color32 bottom = Mean(mirrorPixels, Width / 2 - 8, (int)(Height / 2 - 0.8f * radiusPx) - 8, 16, 16);
                    Debug.Log($"[LookCapture] {name}: mirror ball top {top.r}/{top.g}/{top.b}, bottom {bottom.r}/{bottom.g}/{bottom.b}; sky zenith sRGB {Srgb(look.State.Zenith)}, horizon {Srgb(look.State.Horizon)}, land {Srgb(look.State.Ground)}");
                }

                // Costs, under the day preset (see Cost).
                look.Select(0);
                Cost(camera, data, look, "belt 200 m", BeltCamera, Vector3.forward, msaa);
                Cost(camera, data, look, "S04 patch", patchEye, new Vector3(Mathf.Cos(down), -Mathf.Sin(down), 0f), msaa);
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                single.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(single);
                Object.DestroyImmediate(readback);
            }
            Debug.Log($"[LookCapture] done: {folder}");
        }

        /// <summary>SMAA at high quality, which URP runs only with the camera's post-processing on; off restores the capture's camera.</summary>
        static void Smaa(UniversalAdditionalCameraData data, bool on)
        {
            data.renderPostProcessing = on;
            data.antialiasing = on ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        static void Aim(Camera camera, Vector3 position, Vector3 forward, LookSetup look, float fov = VerticalFovDeg)
        {
            camera.fieldOfView = fov;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Mathf.Abs(forward.y) > 0.99f ? Vector3.forward : Vector3.up));
            look.UpdateFog(camera);
        }

        static Texture2D Render(Camera camera, RenderTexture target, Texture2D readback)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
            {
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;
            }
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            RenderTexture.active = null;
            return readback;
        }

        /// <summary>
        /// What MSAA, the fog, the sky and SMAA cost: renders at 3840 x 2160 (four 1080p frames of pixels), eight per GPU
        /// sync so the GPU's work dominates the CPU's, the five settings interleaved over 15 rounds, each the median; a
        /// setting's cost is its difference to the render as set (SMAA's: with post on, against MSAA 1 without), divided
        /// by four for 1080p. Batchmode wall clock, not PLT-07.
        /// </summary>
        static void Cost(Camera camera, UniversalAdditionalCameraData data, LookSetup look, string view, Vector3 eye, Vector3 forward, int msaa)
        {
            const int Batch = 8, Rounds = 15;
            Aim(camera, eye, forward, look);
            var full = new RenderTexture(2 * Width, 2 * Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = msaa };
            var single = new RenderTexture(2 * Width, 2 * Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            var one = new Texture2D(1, 1, TextureFormat.RGB24, false, false);
            var samples = new List<double>[5];
            for (int c = 0; c < 5; c++)
                samples[c] = new List<double>();
            try
            {
                for (int round = -1; round < Rounds; round++) // round -1 warms each setting up
                    for (int c = 0; c < 5; c++)
                    {
                        RenderSettings.fog = c != 2;
                        camera.clearFlags = c == 3 ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
                        Smaa(data, c == 4);
                        var request = new RenderPipeline.StandardRequest { destination = c == 1 || c == 4 ? single : full };
                        var watch = Stopwatch.StartNew();
                        for (int i = 0; i < Batch; i++)
                            RenderPipeline.SubmitRenderRequest(camera, request);
                        RenderTexture.active = request.destination;
                        one.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
                        RenderTexture.active = null;
                        if (round >= 0)
                            samples[c].Add(watch.Elapsed.TotalMilliseconds / Batch);
                    }
            }
            finally
            {
                RenderSettings.fog = true;
                camera.clearFlags = CameraClearFlags.Skybox;
                Smaa(data, false);
                full.Release();
                single.Release();
                Object.DestroyImmediate(full);
                Object.DestroyImmediate(single);
                Object.DestroyImmediate(one);
            }
            double[] median = samples.Select(list => list.OrderBy(t => t).ElementAt(list.Count / 2)).ToArray();
            double Spread(int c) => samples[c].OrderBy(t => t).ElementAt(Rounds * 9 / 10) - samples[c].OrderBy(t => t).ElementAt(Rounds / 10);
            Debug.Log($"[LookCapture] cost {view} (day): {median[0]:0.000} ms a 3840x2160 render as set (p10-p90 spread {Spread(0):0.000} ms); " +
                      $"for 1080p MSAA {msaa}x {(median[0] - median[1]) / 4:+0.000;-0.000} ms, fog {(median[0] - median[2]) / 4:+0.000;-0.000} ms, sky {(median[0] - median[3]) / 4:+0.000;-0.000} ms, SMAA high with post {(median[4] - median[1]) / 4:+0.000;-0.000} ms " +
                      $"({Batch} renders a sync, median of {Rounds} interleaved rounds, batchmode wall clock)");
        }

        /// <summary>
        /// The belt's band: pixels the trees change (more than 2 levels in any channel against the render without them)
        /// are the band; its crowns are the band's upper 60 %, its sky the rows from 2 to 30 above the band's top over
        /// the band's columns, from the render without the trees. Luma is BT.601 on the encoded values; the ink is the
        /// band's summed linear luminance deficit against the open render, which MSAA's partial pixels keep.
        /// </summary>
        static void Band(string preset, string pass, Color32[] belt, Color32[] open)
        {
            int top = -1, bottom = Height, left = Width, right = -1, count = 0;
            var mask = new bool[belt.Length];
            double ink = 0.0;
            for (int i = 0; i < belt.Length; i++)
            {
                Color32 a = belt[i], b = open[i];
                ink += LinearLuminance(b) - LinearLuminance(a);
                if (Math.Abs(a.r - b.r) <= 2 && Math.Abs(a.g - b.g) <= 2 && Math.Abs(a.b - b.b) <= 2)
                    continue;
                mask[i] = true;
                count++;
                int x = i % Width, y = i / Width; // y up from the bottom row
                top = Math.Max(top, y);
                bottom = Math.Min(bottom, y);
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
            if (count == 0)
            {
                Debug.LogError($"[LookCapture] {preset} {pass}: the trees changed no pixel");
                return;
            }
            int crownFrom = bottom + (int)(0.4f * (top - bottom));
            double crown = 0.0, sky = 0.0;
            int crownCount = 0, skyCount = 0, gapColumns = 0;
            for (int x = left; x <= right; x++)
            {
                bool any = false;
                for (int y = crownFrom; y <= top; y++)
                    if (mask[y * Width + x])
                    {
                        crown += Luma(belt[y * Width + x]);
                        crownCount++;
                        any = true;
                    }
                if (!any)
                    gapColumns++;
                for (int y = top + 2; y <= Math.Min(Height - 1, top + 30); y++)
                {
                    sky += Luma(open[y * Width + x]);
                    skyCount++;
                }
            }
            crown /= Math.Max(1, crownCount);
            sky /= Math.Max(1, skyCount);
            Debug.Log($"[LookCapture] {preset} belt 200 m {pass}: band {right - left + 1} x {top - bottom + 1} px, {count} px; crowns luma {crown:0.0} against the sky above {sky:0.0}: " +
                      $"ratio {crown / sky:0.000}, margin {sky - crown:0.0} levels; columns without crown {gapColumns} of {right - left + 1}; ink {ink:0.0}");
        }

        static GameObject Place(string prefabPath, string metaPath, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) ?? throw new FileNotFoundException($"{prefabPath} is missing: run the catalog import (and git lfs checkout) first");
            Review.RequireRenderable(prefab, prefab.name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // A surface lifted onto the floor, as the review capture stages it (its mat's mean top at the origin).
            float lift = metaPath != null ? Mathf.Max(0f, -ReviewMeta.Read(Review.ProjectPath(metaPath)).Bounds.min.y) : 0f;
            instance.transform.SetPositionAndRotation(position + Vector3.up * lift, Quaternion.Euler(0f, yaw, 0f));
            return instance;
        }

        static void Quad(string name, Vector3 position, float size, Material material)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane); // 10 x 10 m
            plane.name = name;
            Object.DestroyImmediate(plane.GetComponent<Collider>());
            plane.transform.position = position;
            plane.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
            plane.GetComponent<MeshRenderer>().sharedMaterial = material;
            plane.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material Lit(Color linearAlbedo, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", linearAlbedo.gamma);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            return material;
        }

        static void Save(Texture2D texture, string path)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Debug.Log($"[LookCapture] wrote {path}");
        }

        static double Luma(Color32 c) => 0.299 * c.r + 0.587 * c.g + 0.114 * c.b;

        static double LinearLuminance(Color32 c) =>
            0.2126 * Mathf.GammaToLinearSpace(c.r / 255f) + 0.7152 * Mathf.GammaToLinearSpace(c.g / 255f) + 0.0722 * Mathf.GammaToLinearSpace(c.b / 255f);

        static float MeanLinear(Color32[] pixels, int x0, int y0, int w, int h)
        {
            double sum = 0.0;
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    sum += LinearLuminance(pixels[y * Width + x]);
            return (float)(sum / (w * h));
        }

        static Color32 Mean(Color32[] pixels, int x0, int y0, int w, int h)
        {
            double r = 0, g = 0, b = 0;
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                {
                    Color32 c = pixels[y * Width + x];
                    r += c.r;
                    g += c.g;
                    b += c.b;
                }
            int n = w * h;
            return new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
        }

        static string Srgb(Color linear) => $"{255f * Mathf.LinearToGammaSpace(linear.r):0}/{255f * Mathf.LinearToGammaSpace(linear.g):0}/{255f * Mathf.LinearToGammaSpace(linear.b):0}";
    }
}
