using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Fpv450.App;
using Fpv450.Assets;
using Fpv450.Feed;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Wind;
using Fpv450.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Fpv450
{
    /// <summary>
    /// The wet look's evidence (w21-rain-on-camera 1.3): each map's still view (MapStills.json's pose, the farmland's
    /// Explore spawn) Sunny, Rain and Severe rain at the same light (the test range's and ХАНТЕР-09's overcast preset, the
    /// rain's; the farmland's own), as the scene's camera draws it before the feed, into the folder after <c>-out</c>:
    /// <c>&lt;map&gt;-sunny.png</c>, <c>-rain.png</c>, <c>-severe.png</c> (with the fog and the rain's extinction at the class's
    /// middle rate), and <c>&lt;map&gt;-patch.png</c> (the sunny still with the measured patch outlined).
    /// <para>
    /// The law's check, per map on a ground patch, with the fog, the extinction and post off: the dry reference through the
    /// same wet shaders at identity (<see cref="WetLook.Apply"/>'s armed dry), checked against the true Sunny render; then
    /// each state with the specular off (WetLaw.hlsl's probe 1) and the diffuse light alone (probe 2, a white body). Per
    /// pixel, the dry albedo A = L_dry / I, the law's wet light Body(A)·I, against the wet render's; over the patch, the
    /// luminance ratio measured against the law's, and the whole picture's ratio with the specular on.
    /// </para>
    /// <para>
    /// <see cref="Cost"/>: the wet look's frame cost on each view, the state's renders interleaved with Sunny's.
    /// <c>-batchmode -quit -executeMethod Fpv450.WetStills.Run -out &lt;folder&gt; [-maps test-range,farmland,hunter-09]</c>;
    /// needs graphics (never <c>-nographics</c>); exit code 1 on any error.
    /// </para>
    /// </summary>
    public static class WetStills
    {
        const string Tag = "[WetStills]";
        const int Width = 1280, Height = 720;
        const int OvercastPreset = 2; // LookSetup's presets: 0 day, 1 low sun, 2 overcast, 3 night

        /// <summary>A map's view and its ground patch: the patch is a rectangle of the frame (0-1, from the bottom left), on
        /// one ground surface, chosen from the view's sunny still.</summary>
        sealed class View
        {
            public string Id, Scene, Pose, Patch;
            public Vector3 Position;
            public float YawDeg, PitchDeg;
            public Rect PatchRect;
        }

        static readonly View[] Views =
        {
            new View { Id = "test-range", PatchRect = new Rect(0.40f, 0.04f, 0.20f, 0.16f), Patch = "the yard's ground in the foreground" },
            new View { Id = "farmland", PatchRect = new Rect(0.40f, 0.04f, 0.20f, 0.16f), Patch = "the ground in front of the stand" },
            new View { Id = "hunter-09", PatchRect = new Rect(0.40f, 0.04f, 0.20f, 0.16f), Patch = "the meadow in the foreground" },
        };

        public static void Run()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-out");
                if (at < 0 || at + 1 >= args.Length)
                    throw new ArgumentException("usage: -executeMethod Fpv450.WetStills.Run -out <folder> [-maps a,b]");
                string folder = Path.GetFullPath(args[at + 1]);
                int m = Array.IndexOf(args, "-maps");
                string[] maps = m >= 0 && m + 1 < args.Length ? args[m + 1].Split(',') : Views.Select(v => v.Id).ToArray();
                Directory.CreateDirectory(folder);
                Poses();
                Debug.Log($"{Tag} GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, " +
                          $"{Width}x{Height}; law: T {WetOptics.EntryTransmission:0.0000}, p {WetOptics.InternalReflection:0.0000}, c {WetOptics.GrainAbsorption:0.0000}, " +
                          $"F0 {WetOptics.WaterF0:0.0000}, standing water smoothness {WetOptics.PondSmoothness:0.000} on slopes under {WetOptics.DrainSlope:P0}");
                foreach (View v in Views.Where(v => maps.Contains(v.Id)))
                    Capture(v, folder);
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
            finally
            {
                WetLook.Restore();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        // The maps' scenes and poses from MapStills.json, as the stills take them.
        static void Poses()
        {
            Dictionary<string, object> root = Json.Obj(Json.Parse(File.ReadAllText(MapStills.ParamsPath)), MapStills.ParamsPath);
            foreach (object entry in Json.Arr(root, "maps", MapStills.ParamsPath))
            {
                Dictionary<string, object> m = Json.Obj(entry, MapStills.ParamsPath);
                View v = Views.FirstOrDefault(x => x.Id == Json.Str(m, "id", MapStills.ParamsPath));
                if (v == null) continue;
                v.Scene = Json.Str(m, "scene", v.Id);
                v.Pose = Json.Str(m, "pose", v.Id);
                Dictionary<string, object> pose = m;
                if (m.ContainsKey("candidates"))
                {
                    string use = Json.Str(m, "candidate", v.Id);
                    pose = Json.Arr(m, "candidates", v.Id).Select(c => Json.Obj(c, v.Id)).First(c => Json.Str(c, "name", v.Id) == use);
                }
                if (v.Pose == "params")
                {
                    v.Position = Json.Vec3(pose, "position_m", v.Id);
                    v.YawDeg = Json.Num(pose, "yaw_deg", v.Id);
                    v.PitchDeg = Json.Num(pose, "pitch_deg", v.Id);
                }
            }
        }

        static void Capture(View v, string folder)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("no graphics device: run Unity without -nographics");
            ShaderUtil.allowAsyncCompilation = false;
            WetLook.Restore();
            Scene scene = EditorSceneManager.OpenScene(v.Scene, OpenSceneMode.Single);
            Camera camera = SetUp(v, scene);
            var data = camera.GetUniversalAdditionalCameraData();
            bool post = data.renderPostProcessing;
            var hdr = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            var ldr = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                MaterialRow[] materials = PhysicsMaterials.Parse(File.ReadAllText(AppData.File(PhysicsMaterials.RelativePath)), out _);
                SurfaceTable surfaces = SurfaceTable.Parse(File.ReadAllText(AppData.File(SurfaceTable.RelativePath)));
                List<Renderer> hard = WetSurfaces.Hard(materials, surfaces);
                Debug.Log($"{Tag} {v.Id}: {v.Scene}, camera at {camera.transform.position} looking {camera.transform.forward}, fog {RenderSettings.fogMode} " +
                          $"{RenderSettings.fogDensity:0.000000} colour {RenderSettings.fogColor.linear}, {hard.Count} hard renderers");

                // The stills as the flight draws them: fog, extinction (the class's middle rate) and the camera's post.
                float rainBeta = WetSurfaces.ExtinctionPerM(RainLevel.Rain, WetSurfaces.ClassMiddle(RainLevel.Rain));
                float severeBeta = WetSurfaces.ExtinctionPerM(RainLevel.SevereRain, WetSurfaces.ClassMiddle(RainLevel.SevereRain));
                Color[] sunny = Still(camera, ldr, Path.Combine(folder, v.Id + "-sunny.png"));
                WetLook.Apply(WetState.Wet, rainBeta, hard);
                Still(camera, ldr, Path.Combine(folder, v.Id + "-rain.png"));
                WetLook.Apply(WetState.Saturated, severeBeta, hard);
                Still(camera, ldr, Path.Combine(folder, v.Id + "-severe.png"));
                WetLook.Restore();
                Outline(sunny, v.PatchRect, Path.Combine(folder, v.Id + "-patch.png"));
                Debug.Log($"{Tag} {v.Id}: stills Sunny, Rain ({rainBeta * 1000f:0.000}/km at {WetSurfaces.ClassMiddle(RainLevel.Rain):0.0} mm/h) and Severe rain " +
                          $"({severeBeta * 1000f:0.000}/km at {WetSurfaces.ClassMiddle(RainLevel.SevereRain):0.0} mm/h) in {folder}");

                // The law's check: no fog, no extinction, no post.
                bool fog = RenderSettings.fog;
                RenderSettings.fog = false;
                data.renderPostProcessing = false;
                try
                {
                    Color[] dryTrue = Linear(camera, hdr);
                    WetLook.Apply(WetState.Dry, 0f, hard, armedDry: true);
                    Color[] dryArmed = Linear(camera, hdr);
                    WetLook.SetProbe(1);
                    Color[] dryDiffuse = Linear(camera, hdr);
                    WetLook.SetProbe(2);
                    Color[] light = Linear(camera, hdr);
                    Report(v, "armed dry against Sunny", Compare(dryTrue, dryArmed, v.PatchRect));

                    foreach (WetState state in new[] { WetState.Wet, WetState.Saturated })
                    {
                        WetLook.Apply(state, 0f, hard);
                        Color[] whole = Linear(camera, hdr);
                        WetLook.SetProbe(1);
                        Color[] diffuse = Linear(camera, hdr);
                        Law(v, state, v.Patch, v.PatchRect, dryTrue, dryDiffuse, light, whole, diffuse);
                        foreach ((string name, Rect cell) in Cells())
                            Law(v, state, name, cell, dryTrue, dryDiffuse, light, whole, diffuse);
                    }
                }
                    // The wind's sway between renders (the vegetation moves on the editor's clock): Sunny against itself, later.
                    WetLook.Restore();
                    Report(v, "Sunny against itself after the renders (the sway)", Compare(dryTrue, Linear(camera, hdr), v.PatchRect));
                }
                finally
                {
                    RenderSettings.fog = fog;
                    WetLook.Restore();
                }

                Cost(v, camera, hard, rainBeta, severeBeta);
            }
            finally
            {
                data.renderPostProcessing = post;
                camera.targetTexture = null;
                hdr.Release();
                ldr.Release();
                Object.DestroyImmediate(hdr);
                Object.DestroyImmediate(ldr);
            }
        }

        // The scene's camera at the map's pose, under the light the stills take.
        static Camera SetUp(View v, Scene scene)
        {
            if (v.Pose == "explore")
            {
                if (FarmlandExplore.Open(scene) == null)
                    throw new InvalidOperationException("FarmlandExplore.Open failed");
                return Camera.main ?? throw new InvalidOperationException($"{v.Scene}: no main camera");
            }
            foreach (MapGround ground in Object.FindObjectsByType<MapGround>(FindObjectsSortMode.None))
                ground.Draw();
            LookSetup look = Object.FindFirstObjectByType<LookSetup>();
            if (look != null)
                look.Select(OvercastPreset); // the rain's light, for all three
            Camera camera = Camera.main;
            if (camera == null || camera.gameObject.scene != scene)
                throw new InvalidOperationException($"{v.Scene} has no main camera");
            camera.transform.SetPositionAndRotation(v.Position, Quaternion.Euler(v.PitchDeg, v.YawDeg, 0f));
            if (look != null)
                look.UpdateFog(camera);
            return camera;
        }

        static void Render(Camera camera, RenderTexture target)
        {
            camera.targetTexture = target;
            for (int i = 0; i < 2; i++) // the first warms the shaders and the trees
                camera.Render();
        }

        static Color[] Read(RenderTexture target, TextureFormat format)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, format, false, format != TextureFormat.RGB24);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            image.Apply();
            RenderTexture.active = previous;
            Color[] pixels = image.GetPixels();
            Object.DestroyImmediate(image);
            return pixels;
        }

        static Color[] Linear(Camera camera, RenderTexture hdr)
        {
            Render(camera, hdr);
            return Read(hdr, TextureFormat.RGBAHalf);
        }

        static Color[] Still(Camera camera, RenderTexture ldr, string png)
        {
            Render(camera, ldr);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = ldr;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(png, image.EncodeToPNG());
            Color[] pixels = image.GetPixels();
            Object.DestroyImmediate(image);
            return pixels;
        }

        // The lower half's cells, four across and two up, each checked too: the patch is chosen among them by eye.
        static IEnumerable<(string name, Rect cell)> Cells()
        {
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 4; col++)
                    yield return ($"cell {col},{row} (from the bottom left)", new Rect(col * 0.25f, row * 0.25f, 0.25f, 0.25f));
        }

        static void Outline(Color[] pixels, Rect patch, string png)
        {
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var copy = (Color[])pixels.Clone();
            foreach ((string _, Rect cell) in Cells())
                Frame(copy, Pixels(cell), Color.yellow, 1);
            Frame(copy, Pixels(patch), Color.magenta, 2);
            image.SetPixels(copy);
            image.Apply();
            File.WriteAllBytes(png, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        static void Frame(Color[] image, RectInt r, Color colour, int width)
        {
            for (int x = r.xMin; x < r.xMax; x++)
                for (int k = 0; k < width; k++)
                {
                    image[(r.yMin + k) * Width + x] = colour;
                    image[(r.yMax - 1 - k) * Width + x] = colour;
                }
            for (int y = r.yMin; y < r.yMax; y++)
                for (int k = 0; k < width; k++)
                {
                    image[y * Width + r.xMin + k] = colour;
                    image[y * Width + r.xMax - 1 - k] = colour;
                }
        }

        static RectInt Pixels(Rect r) =>
            new RectInt(Mathf.RoundToInt(r.x * Width), Mathf.RoundToInt(r.y * Height), Mathf.RoundToInt(r.width * Width), Mathf.RoundToInt(r.height * Height));

        static float Y(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        // The whole picture's difference, then the patch's.
        static string Compare(Color[] a, Color[] b, Rect patch)
        {
            RectInt r = Pixels(patch);
            double max = 0, sum = 0, patchMax = 0;
            int differ = 0, patchDiffer = 0, patchCount = 0;
            for (int i = 0; i < a.Length; i++)
            {
                double d = Math.Max(Math.Abs(a[i].r - b[i].r), Math.Max(Math.Abs(a[i].g - b[i].g), Math.Abs(a[i].b - b[i].b)));
                if (d > 0) differ++;
                max = Math.Max(max, d);
                sum += d;
                int x = i % Width, y = i / Width;
                if (x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax)
                {
                    patchCount++;
                    if (d > 0) patchDiffer++;
                    patchMax = Math.Max(patchMax, d);
                }
            }
            return string.Format(CultureInfo.InvariantCulture,
                "{0} of {1} pixels differ, the largest by {2:0.000000}, the mean {3:0.000000} (linear); on the patch {4} of {5}, the largest by {6:0.000000}",
                differ, a.Length, max, sum / a.Length, patchDiffer, patchCount, patchMax);
        }

        // The law per pixel over the patch, and the whole picture with its specular.
        static void Law(View v, WetState state, string what, Rect patch, Color[] dryWhole, Color[] dryDiffuse, Color[] light, Color[] wetWhole, Color[] wetDiffuse)
        {
            RectInt r = Pixels(patch);
            double sumDry = 0, sumWet = 0, sumLaw = 0, sumA = 0, sumWhole = 0, sumWholeDry = 0, worst = 0;
            int n = 0;
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    int i = y * Width + x;
                    Color d = dryDiffuse[i], e = light[i], w = wetDiffuse[i];
                    if (Y(e) < 1e-4f) continue; // unlit: no albedo to read
                    var law = new Color(LawAt(d.r, e.r), LawAt(d.g, e.g), LawAt(d.b, e.b));
                    sumDry += Y(d);
                    sumWet += Y(w);
                    sumLaw += Y(law);
                    sumA += Y(d) / Y(e);
                    sumWhole += Y(wetWhole[i]);
                    sumWholeDry += Y(dryWhole[i]);
                    if (Y(law) > 1e-4f)
                        worst = Math.Max(worst, Math.Abs(Y(w) / Y(law) - 1.0));
                    n++;
                }
            double albedo = sumA / Math.Max(1, n);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "{0} {1} {2}: {3}, {4} pixels, mean drawn albedo {5:0.0000}: diffuse luminance ratio measured {6:0.0000}, law {7:0.0000} " +
                "(per pixel at most {8:0.00%} off; Body(A)/A at the mean {9:0.0000}); the whole picture with its specular {10:0.0000}",
                Tag, v.Id, state, what, n, albedo, sumWet / sumDry, sumLaw / sumDry, worst, WetOptics.Body(albedo) / albedo, sumWhole / sumWholeDry));
        }

        // The law's wet light at a pixel whose diffuse light is i and dry diffuse radiance d: Body(d / i) · i.
        static float LawAt(float d, float i) => i > 1e-6f ? (float)(WetOptics.Body(d / i) * i) : 0f;

        static void Report(View v, string what, string detail) => Debug.Log($"{Tag} {v.Id} {what}: {detail}");

        /// <summary>
        /// The wet look's frame cost on the view: renders at 1920 x 1080, eight per GPU sync so the GPU's work dominates,
        /// Sunny, Rain and Severe rain (with the class's middle extinction) interleaved over 15 rounds, each the median;
        /// a state's cost is its difference to Sunny. Batchmode wall clock, the editor's; the player's PLT-07 run is the
        /// farmland bench.
        /// </summary>
        static void Cost(View v, Camera camera, List<Renderer> hard, float rainBeta, float severeBeta)
        {
            const int Batch = 8, Rounds = 15;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var one = new Texture2D(1, 1, TextureFormat.RGB24, false, false);
            var samples = new List<double>[3];
            for (int c = 0; c < 3; c++)
                samples[c] = new List<double>();
            try
            {
                camera.targetTexture = target;
                for (int round = -1; round < Rounds; round++)
                    for (int c = 0; c < 3; c++)
                    {
                        if (c == 0) WetLook.Restore();
                        else WetLook.Apply(c == 1 ? WetState.Wet : WetState.Saturated, c == 1 ? rainBeta : severeBeta, hard);
                        var watch = Stopwatch.StartNew();
                        for (int i = 0; i < Batch; i++)
                            camera.Render();
                        RenderTexture.active = target;
                        one.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
                        RenderTexture.active = null;
                        if (round >= 0)
                            samples[c].Add(watch.Elapsed.TotalMilliseconds / Batch);
                    }
            }
            finally
            {
                WetLook.Restore();
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(one);
            }
            double Median(int c) => samples[c].OrderBy(t => t).ElementAt(Rounds / 2);
            double Spread(int c) => samples[c].OrderBy(t => t).ElementAt(Rounds * 9 / 10) - samples[c].OrderBy(t => t).ElementAt(Rounds / 10);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "{0} {1} cost: Sunny {2:0.000} ms a 1920x1080 render (p10-p90 {3:0.000}), Rain {4:+0.000;-0.000} ms (p10-p90 {5:0.000}), Severe rain {6:+0.000;-0.000} ms " +
                "(p10-p90 {7:0.000}); {8} renders a sync, median of {9} interleaved rounds, batchmode wall clock",
                Tag, v.Id, Median(0), Spread(0), Median(1) - Median(0), Spread(1), Median(2) - Median(0), Spread(2), Batch, Rounds));
        }
    }
}
