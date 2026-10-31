using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Fpv450.Feed
{
    /// <summary>
    /// The feed's batch captures (p4-feed task 1.2), with graphics, never <c>-nographics</c>; exit code 1 on failure.
    /// <list type="bullet">
    /// <item><c>-executeMethod Fpv450.Feed.FeedCapture.Stills -feedField a-b[+cut|+hardcut][,...]</c>: the Unity side of
    /// the Godot feed's <c>--video-field a-b[+cut|+hardcut]</c> (VideoFeed.cs:77-93, 174-197), one PNG per field,
    /// <c>feed-&lt;seed&gt;-&lt;field&gt;.png</c>, in a folder per range when there are several. Options: <c>-feedSeed n</c>, <c>-feedInput frame.png</c> (one
    /// identical input frame instead of the test scene's render, FEED-00), <c>-feedOut dir</c> (default
    /// <c>unity/Logs/feed</c>), <c>-feedSize WxH</c> (1920x1080), <c>-feedLens on|off</c>, <c>-feedSceneWidth n</c>,
    /// <c>-feedFieldWidth n</c>.</item>
    /// <item><c>-executeMethod Fpv450.Feed.FeedCapture.Lens</c>: the lens scenario. A straight-line grid through
    /// the feed, its lines traced, and k1 fitted the way K§11.4 measured it off the footage; exits 1 unless it lies
    /// within ±0.03 of 0.33.</item>
    /// </list>
    /// Signals are the stubs (gain 1, link margin 1, current 0), as the Godot captures pin them with
    /// <c>--video-signals 1,1,0</c>, and the OSD is the stub's Godot layout at the Godot capture's pinned values
    /// (altitude, distance and timer 0, 25.2 V, 0 A). The A/B against Godot takes <c>-feedLens off</c>: Godot had no
    /// lens.
    /// </summary>
    public static class FeedCapture
    {
        const float K1Measured = 0.33f, K1Tolerance = 0.03f; // K§11.4 O1, FEED-12

        public static void Stills() => Run(RunStills);

        public static void Lens() => Run(RunLens);

        static void Run(Action body)
        {
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                Debug.Log($"[Feed] GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), " +
                          $"colour space {QualitySettings.activeColorSpace}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
                body();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Feed] FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
        }

        // --- the stills ------------------------------------------------------------------------------------------

        static void RunStills()
        {
            string[] args = Environment.GetCommandLineArgs();
            string ranges = Arg(args, "-feedField") ?? throw new ArgumentException("-feedField a-b[+cut|+hardcut][,...] is required");
            int seed = int.Parse(Arg(args, "-feedSeed") ?? "1", CultureInfo.InvariantCulture);
            string input = Arg(args, "-feedInput");
            string outDir = Path.GetFullPath(Arg(args, "-feedOut") ?? Path.Combine(Application.dataPath, "..", "Logs", "feed"));
            Vector2Int size = ParseSize(Arg(args, "-feedSize") ?? "1920x1080");

            FeedSettings settings = Settings(args, seed);
            FpvCamera rig;
            Texture2D still = null;
            if (input != null)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                rig = NewRig(settings, Vector3.zero);
                still = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                if (!still.LoadImage(File.ReadAllBytes(input)))
                    throw new IOException($"{input} did not load as an image");
            }
            else
            {
                EditorSceneManager.OpenScene(FeedSetup.TestScenePath, OpenSceneMode.Single);
                rig = Object.FindAnyObjectByType<FpvCamera>()
                    ?? throw new InvalidOperationException($"{FeedSetup.TestScenePath} has no FpvCamera");
                rig.settings = settings;
            }

            var target = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var readback = new Texture2D(size.x, size.y, TextureFormat.RGB24, false, false);
            try
            {
                // Each range is one Godot run: a fresh scheduler from the seed, and its own folder when there are several.
                string[] list = ranges.Split(',');
                foreach (string range in list)
                {
                    ParseField(range, out int from, out int to, out string cut);
                    string dir = list.Length > 1 ? Path.Combine(outDir, range.Replace('+', '_')) : outDir;
                    Directory.CreateDirectory(dir);
                    rig.pinnedField = from - 1; // Begin advances to the field before the range, as Godot's capture did
                    rig.Begin();
                    try
                    {
                        rig.Output.targetTexture = target;
                        if (still != null)
                            rig.ShowStill(still, encoded: true);
                        Debug.Log($"[Feed] capturing fields {range}, seed {seed}, " +
                                  $"input {(input != null ? $"{Path.GetFileName(input)} {still.width}x{still.height}" : $"{FeedSetup.TestScenePath} rendered {settings.SceneSize.x}x{settings.SceneSize.y}")}, " +
                                  $"output {size.x}x{size.y}, field {settings.fieldWidth}x{FeedSettings.FieldRows}, lens k1 {settings.K1:0.00}. " +
                                  FeedSchedule.Summary(seed, 15000, 0f));
                        if (cut != null)
                        {
                            // VideoFeed.cs:180-184: the cut fires after the field before the range.
                            rig.Events.TriggerCut(cut == "hardcut");
                        }
                        for (int f = from; f <= to; f++)
                        {
                            rig.Step(f);
                            if (still == null)
                                rig.GetComponent<Camera>().Render();
                            rig.Output.Render();
                            RenderTexture.active = target;
                            readback.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0, false);
                            RenderTexture.active = null;
                            string file = Path.Combine(dir, $"feed-{seed}-{f:0000}.png");
                            File.WriteAllBytes(file, readback.EncodeToPNG());
                            Debug.Log($"[Feed] {file}: {Describe(rig.Events)}");
                        }
                    }
                    finally
                    {
                        rig.End();
                    }
                }
            }
            finally
            {
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
                if (still != null)
                    Object.DestroyImmediate(still);
            }
            EditorApplication.Exit(0);
        }

        /// The settings asset with the command line's overrides, as a copy so the asset stays as it is.
        static FeedSettings Settings(string[] args, int seed)
        {
            var asset = AssetDatabase.LoadAssetAtPath<FeedSettings>(FeedSetup.SettingsPath)
                ?? throw new FileNotFoundException($"{FeedSetup.SettingsPath} is missing: run Fpv450.Feed.FeedSetup.Apply");
            FeedSettings s = Object.Instantiate(asset);
            s.hideFlags = HideFlags.DontSave; // a scene change would unload it otherwise
            s.seed = seed;
            string lens = Arg(args, "-feedLens");
            if (lens != null)
                s.lens = lens == "on" ? true : lens == "off" ? false : throw new ArgumentException("-feedLens on|off");
            string w = Arg(args, "-feedSceneWidth");
            if (w != null)
                s.sceneWidth = int.Parse(w, CultureInfo.InvariantCulture);
            w = Arg(args, "-feedFieldWidth");
            if (w != null)
                s.fieldWidth = int.Parse(w, CultureInfo.InvariantCulture);
            return s;
        }

        static FpvCamera NewRig(FeedSettings settings, Vector3 position)
        {
            var go = new GameObject("FpvCamera");
            go.transform.position = position;
            var camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 2000f;
            camera.enabled = false;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.antialiasing = AntialiasingMode.None;
            data.renderPostProcessing = false;
            var rig = go.AddComponent<FpvCamera>();
            rig.settings = settings;
            return rig;
        }

        /// VideoFeed.cs:77-93, the Godot feed's `--video-field` grammar.
        static void ParseField(string arg, out int from, out int to, out string cut)
        {
            cut = null;
            int plus = arg.IndexOf('+');
            if (plus >= 0)
            {
                cut = arg.Substring(plus + 1);
                arg = arg.Substring(0, plus);
                if (cut != "cut" && cut != "hardcut")
                    throw new ArgumentException($"-feedField: '+{cut}' is neither +cut nor +hardcut");
            }
            int dash = arg.IndexOf('-', 1);
            from = int.Parse(dash < 0 ? arg : arg.Substring(0, dash), CultureInfo.InvariantCulture);
            to = dash < 0 ? from : int.Parse(arg.Substring(dash + 1), CultureInfo.InvariantCulture);
            if (to < from || from < 1)
                throw new ArgumentException($"-feedField needs a field or a range like 610-660; got '{arg}'");
        }

        /// VideoFeed.cs:199-210, the Godot capture's per-field line, so the two logs read alike.
        static string Describe(FeedEvents events)
        {
            FeedLoss loss = events.Loss;
            IFormatProvider inv = CultureInfo.InvariantCulture;
            return string.Format(inv, "field {0}: {1}", events.Field, loss.Current)
                + (loss.Current == FeedLoss.State.PartialSnow ? string.Format(inv, " split {0:0.00} top {1:0.00}", loss.Split, loss.TopStrip) : "")
                + (loss.Current == FeedLoss.State.Black ? string.Format(inv, " rows {0:0.00}-{1:0.00}", loss.BlackA, loss.BlackB) : "")
                + (loss.SnowLevel > 0f ? string.Format(inv, " snow {0:0} chroma {1:0.0}", loss.SnowLevel * 255f, loss.SnowChroma) : "")
                + (loss.Tell > 0f ? string.Format(inv, ", tell +{0:0} %", loss.Tell * 100f) : "")
                + (loss.TearAmp > 0f ? string.Format(inv, ", tear {0:0.00} top {1:0}", loss.TearAmp, loss.TearTop) : "")
                + (loss.GrainBoost > 0f ? string.Format(inv, ", grain +{0:0} %", loss.GrainBoost * 100f) : "")
                + string.Format(inv, ", N3 {0:0.000}, N4 {1:0.000}, degrade {2:0.00}", events.N3Amp, events.N4Amp, events.Degrade);
        }

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static Vector2Int ParseSize(string s)
        {
            string[] p = s.Split('x');
            return new Vector2Int(int.Parse(p[0], CultureInfo.InvariantCulture), int.Parse(p[1], CultureInfo.InvariantCulture));
        }

        // --- the lens scenario -----------------------------------------------------------------------------------

        // The grid: a white wall 10 m ahead, black horizontal bars at these heights of the pinhole image (in
        // half-widths, where they bow most) and vertical bars at these azimuths, all 0.025 half-widths thick.
        static readonly float[] BarHeights = { -0.48f, -0.34f, 0.34f, 0.48f };
        static readonly float[] BarAzimuthsDeg = { -45f, -30f, 30f, 45f };
        const float WallM = 10f, BarHalfWidths = 0.025f;
        // A quiet field: seed 1 field 664 has no N3, no N4, no quality drop and no loss (feedbench schedule).
        const int QuietSeed = 1, QuietField = 664;

        static void RunLens()
        {
            const int w = 1920, h = 1080;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var asset = AssetDatabase.LoadAssetAtPath<FeedSettings>(FeedSetup.SettingsPath)
                ?? throw new FileNotFoundException($"{FeedSetup.SettingsPath} is missing: run Fpv450.Feed.FeedSetup.Apply");
            FeedSettings settings = Object.Instantiate(asset);
            settings.hideFlags = HideFlags.DontSave;
            settings.seed = QuietSeed;
            settings.lens = true;
            float f = settings.focalHalfWidths;

            var white = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            white.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f));
            var black = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            black.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.03f));
            float wallHalf = WallM * settings.Coverage / f * 1.2f;
            Quad(new Vector3(0f, 0f, WallM + 0.01f), new Vector2(wallHalf * 2f, wallHalf * 2f), white);
            float thick = BarHalfWidths * WallM / f;
            foreach (float y in BarHeights)
                Quad(new Vector3(0f, y * WallM / f, WallM), new Vector2(wallHalf * 2f, thick), black);
            foreach (float az in BarAzimuthsDeg)
                Quad(new Vector3(WallM * Mathf.Tan(az * Mathf.Deg2Rad), 0f, WallM), new Vector2(thick, 0.3f * WallM / f), black);

            FpvCamera rig = NewRig(settings, Vector3.zero);
            var target = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var readback = new Texture2D(w, h, TextureFormat.RGB24, false, false);
            Color32[] px;
            try
            {
                rig.pinnedField = QuietField;
                rig.Begin();
                rig.Output.targetTexture = target;
                rig.Step(QuietField);
                rig.GetComponent<Camera>().Render();
                rig.Output.Render();
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                RenderTexture.active = null;
                px = readback.GetPixels32();
                string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "feed"));
                Directory.CreateDirectory(outDir);
                File.WriteAllBytes(Path.Combine(outDir, "lens-grid.png"), readback.EncodeToPNG());
            }
            finally
            {
                rig.End();
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
            }

            // Trace each bar: the darkness-weighted centre across it, per column (horizontal bars) or per row near the
            // middle (vertical bars), in half-widths from the picture centre (K§11.4's normalisation).
            float Luma(int x, int yTop)
            {
                Color32 c = px[(h - 1 - yTop) * w + x];
                return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            }
            float half = w / 2f;
            var lines = new List<List<Vector2>>();
            foreach (float yu in BarHeights)
            {
                var pts = new List<Vector2>();
                for (int x = 40; x < w - 40; x += 4)
                {
                    float xd = (x + 0.5f - half) / half;
                    float yd = DistortedHeight(yu, xd, settings.K1); // where the bar should cross this column
                    int yc = Mathf.RoundToInt(h / 2f + yd * half - 0.5f);
                    if (Centroid(v => Luma(x, v), yc, 60, 8, h - 4, out float y))
                        pts.Add(new Vector2(xd, (y + 0.5f - h / 2f) / half));
                }
                lines.Add(pts);
            }
            float k1 = FitK1(lines, out float residualPx);
            residualPx *= half;

            // Each vertical bar's undistorted column against its azimuth gives the focal length, p_u = f tan(az): the
            // rig's own, so a field of view out of step with the lens's coverage shows here, not in k1.
            var focal = new List<(float az, float f)>();
            foreach (float az in BarAzimuthsDeg)
            {
                float sum = 0f;
                int n = 0;
                int xc = Mathf.RoundToInt(half + InvertRadial(f * Mathf.Tan(az * Mathf.Deg2Rad), settings.K1) * half - 0.5f);
                for (int y = h / 2 - 60; y <= h / 2 + 60; y += 4)
                {
                    if (!Centroid(v => Luma(v, y), xc, 60, 0, w, out float x))
                        continue;
                    float xd = (x + 0.5f - half) / half, yd = (y + 0.5f - h / 2f) / half;
                    sum += xd * (1f + k1 * (xd * xd + yd * yd));
                    n++;
                }
                if (n > 0)
                    focal.Add((az, sum / n / Mathf.Tan(az * Mathf.Deg2Rad)));
            }
            float fFit = 0f;
            foreach (var (_, fb) in focal)
                fFit += fb / focal.Count;
            float hfov = 2f * Mathf.Atan((1f + k1) / fFit) * Mathf.Rad2Deg;
            int points = 0;
            foreach (List<Vector2> l in lines)
                points += l.Count;

            bool pass = Mathf.Abs(k1 - K1Measured) <= K1Tolerance;
            string bars = string.Join(", ", focal.ConvertAll(b => string.Format(CultureInfo.InvariantCulture, "{0:0}° f {1:0.000}", b.az, b.f)));
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Feed] lens scenario: k1 {0:0.000} fitted from {1} traced bars ({2} points, straightness residual {3:0.00} px), " +
                "K§11.4 {4:0.00} ± {5:0.00}: {6}. Vertical bars: {7}; focal length {8:0.000} half-widths, picture {9:0.0}° across. " +
                "Settings: k1 {10:0.00}, f {11:0.000}, render {12}x{13} at {14:0.00}° vertical.",
                k1, lines.Count, points, residualPx, K1Measured, K1Tolerance, pass ? "PASS" : "FAIL",
                bars, fFit, hfov, settings.k1, f, settings.SceneSize.x, settings.SceneSize.y, settings.RenderVerticalFovDeg));
            EditorApplication.Exit(pass ? 0 : 1);
        }

        static void Quad(Vector3 centre, Vector2 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = centre;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// The darkness-weighted centre of a dark bar around `guess` along one axis: weight = how far each sample
        /// sits below the brightest sample in the window. False when the window holds no bar.
        static bool Centroid(Func<int, float> luma, int guess, int window, int min, int max, out float centre)
        {
            centre = 0f;
            int a = Mathf.Max(min, guess - window), b = Mathf.Min(max - 1, guess + window);
            float bright = 0f;
            for (int i = a; i <= b; i++)
                bright = Mathf.Max(bright, luma(i));
            float sw = 0f, s = 0f;
            for (int i = a; i <= b; i++)
            {
                float wgt = Mathf.Max(0f, bright - luma(i) - 0.5f * bright);
                sw += wgt;
                s += wgt * i;
            }
            if (sw <= 0f)
                return false;
            centre = s / sw;
            return true;
        }

        /// The distorted height at column `xd` of a straight line at pinhole height `yu`: solves
        /// yu = yd (1 + k1 (xd'² + yd²)) along the line, by fixed-point iteration (xd' the undistorted column).
        static float DistortedHeight(float yu, float xd, float k1)
        {
            float yd = yu;
            for (int i = 0; i < 20; i++)
                yd = yu / (1f + k1 * (xd * xd + yd * yd));
            return yd;
        }

        /// The distorted radius whose undistorted radius is `pu` (on the horizontal axis), by Newton.
        static float InvertRadial(float pu, float k1)
        {
            float x = pu;
            for (int i = 0; i < 30; i++)
                x -= (x * (1f + k1 * x * x) - pu) / (1f + 3f * k1 * x * x);
            return x;
        }

        /// K§11.4's measure: the k1 that makes the traced lines straight once undistorted, p_u = p_d (1 + k1 r_d²).
        /// Golden-section search on the summed squared residual of a straight-line fit per line; `residual` is the
        /// RMS distance from those lines at the best k1, in half-widths.
        static float FitK1(List<List<Vector2>> lines, out float residual)
        {
            double Cost(double k)
            {
                double sum = 0;
                int n = 0;
                foreach (List<Vector2> pts in lines)
                {
                    double sx = 0, sy = 0, sxx = 0, sxy = 0, syy = 0;
                    foreach (Vector2 p in pts)
                    {
                        double s = 1 + k * (p.x * (double)p.x + p.y * (double)p.y);
                        double x = p.x * s, y = p.y * s;
                        sx += x; sy += y; sxx += x * x; sxy += x * y; syy += y * y;
                    }
                    int m = pts.Count;
                    double vx = sxx - sx * sx / m, cxy = sxy - sx * sy / m, vy = syy - sy * sy / m;
                    sum += vy - cxy * cxy / vx; // the residual of y on x
                    n += m;
                }
                return sum / Math.Max(1, n);
            }
            double lo = 0, hi = 1, g = (Math.Sqrt(5) - 1) / 2;
            double c = hi - g * (hi - lo), d = lo + g * (hi - lo);
            for (int i = 0; i < 80; i++)
            {
                if (Cost(c) < Cost(d)) hi = d; else lo = c;
                c = hi - g * (hi - lo);
                d = lo + g * (hi - lo);
            }
            double best = (lo + hi) / 2;
            residual = (float)Math.Sqrt(Cost(best));
            return (float)best;
        }
    }
}
